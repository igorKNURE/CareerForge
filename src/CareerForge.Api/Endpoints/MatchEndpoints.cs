using System.Text.Json;
using CareerForge.Api.Common;
using CareerForge.Api.Validation;
using CareerForge.Application.Abstractions.Matching;
using CareerForge.Application.Abstractions.Persistence;
using CareerForge.Application.JobDescriptions.Models;
using CareerForge.Application.Matching.Models;
using CareerForge.Application.Resumes.Models;
using CareerForge.Domain.Entities;
using CareerForge.Domain.Enums;
using CareerForge.Domain.ValueObjects;
using CareerForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CareerForge.Api.Endpoints;

/// <summary>Endpoints for scoring resumes against vacancies and listing prior match reports.</summary>
public static class MatchEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/matches").WithTags("Matches").RequireAuthorization();

        group.MapPost("/", CreateAsync).WithValidation<CreateMatchRequest>().RequireRateLimiting("llm-heavy");
        group.MapPost("/{id:guid}/rerun", RerunAsync).RequireRateLimiting("llm-heavy");
        group.MapGet("/", ListAsync).WithBrowserCache(30);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

    public sealed record CreateMatchRequest(Guid ResumeId, Guid JobDescriptionId);

    private static async Task<Results<Ok<MatchReportResponse>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> CreateAsync(
        CreateMatchRequest request,
        HttpContext ctx,
        IMatchScorer scorer,
        AppDbContext db,
        ILogger<MatchReport> logger,
        CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var lang = ResolveLanguage(ctx);

        var existing = await db.MatchReports.FirstOrDefaultAsync(
            m => m.UserId == userId && m.ResumeId == request.ResumeId && m.JobDescriptionId == request.JobDescriptionId,
            ct);
        if (existing is not null)
        {
            logger.LogInformation("Match cache hit for user={User} resume={Resume} jd={Jd} → {Id}",
                userId, request.ResumeId, request.JobDescriptionId, existing.Id);
            return TypedResults.Ok(MatchReportResponse.From(existing, null));
        }

        var loaded = await LoadAndScoreAsync(userId, request.ResumeId, request.JobDescriptionId, scorer, db, logger, lang, ct);
        if (loaded.Error is not null) return loaded.Error;

        var report = new MatchReport
        {
            UserId = userId,
            ResumeId = request.ResumeId,
            JobDescriptionId = request.JobDescriptionId,
        };
        ApplyScoringResult(report, loaded.Result!);
        db.MatchReports.Add(report);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Match {Id} scored: overall={Overall}", report.Id, report.OverallScore);
        return TypedResults.Ok(MatchReportResponse.From(report, loaded.Result));
    }

    private static async Task<Results<Ok<MatchReportResponse>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> RerunAsync(
        Guid id,
        HttpContext ctx,
        IMatchScorer scorer,
        AppDbContext db,
        ILogger<MatchReport> logger,
        CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var lang = ResolveLanguage(ctx);
        var report = await db.MatchReports.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);
        if (report is null)
            return TypedResults.NotFound(new ProblemDetails { Title = "Match not found" });

        var loaded = await LoadAndScoreAsync(userId, report.ResumeId, report.JobDescriptionId, scorer, db, logger, lang, ct);
        if (loaded.Error is not null) return loaded.Error;

        ApplyScoringResult(report, loaded.Result!);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Match {Id} re-scored: overall={Overall}", report.Id, report.OverallScore);
        return TypedResults.Ok(MatchReportResponse.From(report, loaded.Result));
    }

    private static string ResolveLanguage(HttpContext ctx)
    {
        var header = ctx.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(header)) return "en";
        var first = header.Split(',', 2)[0].Split(';', 2)[0].Trim();
        return CareerForge.Application.Abstractions.Llm.LanguageInstruction.Normalize(first);
    }

    private static void ApplyScoringResult(MatchReport report, MatchScoringResult r)
    {
        report.OverallScore = r.OverallScore;
        report.SkillCoverageScore = r.SkillCoverageScore;
        report.SemanticSimilarityScore = r.SemanticSimilarityScore;
        report.ExperienceFitScore = r.ExperienceFitScore;
        report.MatchedMustHaveSkills = r.MatchedMustHaveSkills.ToList();
        report.MissingMustHaveSkills = r.MissingMustHaveSkills.ToList();
        report.MatchedNiceToHaveSkills = r.MatchedNiceToHaveSkills.ToList();
        report.Findings = r.Findings.ToList();
        report.ImprovementSummary = r.ImprovementSummary;
    }

    private static async Task<LoadResult> LoadAndScoreAsync(
        Guid userId, Guid resumeId, Guid jdId,
        IMatchScorer scorer, AppDbContext db, ILogger logger, string language, CancellationToken ct)
    {
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.Id == resumeId && r.UserId == userId, ct);
        if (resume is null)
            return new(null, TypedResults.NotFound(new ProblemDetails { Title = "Resume not found" }));
        if (string.IsNullOrWhiteSpace(resume.StructuredJson))
            return new(null, TypedResults.BadRequest(new ProblemDetails { Title = "Resume has not been analysed yet" }));

        var vacancy = await db.JobDescriptions.FirstOrDefaultAsync(j => j.Id == jdId && j.UserId == userId, ct);
        if (vacancy is null)
            return new(null, TypedResults.NotFound(new ProblemDetails { Title = "Vacancy not found" }));
        if (string.IsNullOrWhiteSpace(vacancy.StructuredJson))
            return new(null, TypedResults.BadRequest(new ProblemDetails { Title = "Vacancy has not been analysed yet" }));

        ResumeProfile? resumeProfile;
        JobProfile? vacancyProfile;
        try
        {
            resumeProfile = JsonSerializer.Deserialize<ResumeProfile>(resume.StructuredJson, JsonOpts);
            vacancyProfile = JsonSerializer.Deserialize<JobProfile>(vacancy.StructuredJson, JsonOpts);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Stored profile JSON is malformed for resume {ResumeId} or vacancy {VacancyId}", resume.Id, vacancy.Id);
            return new(null, TypedResults.BadRequest(new ProblemDetails { Title = "Stored profile JSON is malformed", Detail = ex.Message }));
        }
        if (resumeProfile is null || vacancyProfile is null)
            return new(null, TypedResults.BadRequest(new ProblemDetails { Title = "Profile JSON deserialised to null" }));

        var resumeEmb = resume.SummaryEmbedding?.ToArray();
        var vacancyEmb = vacancy.SummaryEmbedding?.ToArray();
        var result = await scorer.ScoreAsync(resumeProfile, vacancyProfile, resumeEmb, vacancyEmb, language, ct);
        return new(result, null);
    }

    private record LoadResult(
        MatchScoringResult? Result,
        Results<Ok<MatchReportResponse>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>? Error);

    // List and detail queries depend on IReadOnlyDb so they can be routed to a read
    // replica without modifying these handlers.
    private static async Task<Ok<List<MatchReportListItem>>> ListAsync(
        HttpContext ctx, IReadOnlyDb db, CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        // Findings is a JSON column; bucketing severity counts is performed in-memory
        // after projection because match-report lists are bounded in size per user.
        var rows = await db.MatchReports
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new { m.Id, m.ResumeId, m.JobDescriptionId, m.OverallScore, m.CreatedAt, m.Findings })
            .ToListAsync(ct);
        var items = rows.Select(m => new MatchReportListItem(
            m.Id, m.ResumeId, m.JobDescriptionId, m.OverallScore, m.CreatedAt,
            m.Findings.Count(f => f.Severity == FindingSeverity.Critical),
            m.Findings.Count(f => f.Severity == FindingSeverity.Warning),
            m.Findings.Count(f => f.Severity == FindingSeverity.Info))).ToList();
        return TypedResults.Ok(items);
    }

    private static async Task<Results<Ok<MatchReportResponse>, NotFound>> GetByIdAsync(
        Guid id, HttpContext ctx, IReadOnlyDb db, CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var report = await db.MatchReports.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);
        return report is null ? TypedResults.NotFound() : TypedResults.Ok(MatchReportResponse.From(report, null));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var report = await db.MatchReports.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);
        if (report is null) return TypedResults.NotFound();
        db.MatchReports.Remove(report);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    public sealed record MatchReportListItem(
        Guid Id,
        Guid ResumeId,
        Guid JobDescriptionId,
        decimal OverallScore,
        DateTime CreatedAt,
        int CriticalCount,
        int WarningCount,
        int InfoCount);

    public sealed record MatchReportResponse(
        Guid Id,
        Guid ResumeId,
        Guid JobDescriptionId,
        decimal OverallScore,
        decimal SkillCoverageScore,
        decimal SemanticSimilarityScore,
        decimal ExperienceFitScore,
        IReadOnlyList<string>? MatchedMustHaveSkills,
        IReadOnlyList<string>? MissingMustHaveSkills,
        IReadOnlyList<string>? MatchedNiceToHaveSkills,
        IReadOnlyList<MatchFinding> Findings,
        string? ImprovementSummary,
        DateTime CreatedAt)
    {
        public static MatchReportResponse From(MatchReport r, MatchScoringResult? _ = null)
            => new(r.Id, r.ResumeId, r.JobDescriptionId,
                r.OverallScore, r.SkillCoverageScore, r.SemanticSimilarityScore, r.ExperienceFitScore,
                r.MatchedMustHaveSkills,
                r.MissingMustHaveSkills,
                r.MatchedNiceToHaveSkills,
                r.Findings,
                r.ImprovementSummary,
                r.CreatedAt);
    }
}
