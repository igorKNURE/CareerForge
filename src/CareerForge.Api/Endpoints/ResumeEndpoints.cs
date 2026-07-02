using System.Text.Json;
using CareerForge.Api.Common;
using CareerForge.Application.Abstractions.BackgroundJobs;
using CareerForge.Application.Abstractions.Documents;
using CareerForge.Application.Abstractions.Llm;
using CareerForge.Application.Abstractions.Resumes;
using CareerForge.Domain.Entities;
using CareerForge.Domain.Enums;
using CareerForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace CareerForge.Api.Endpoints;

/// <summary>Endpoints for uploading resumes, listing them, and triggering re-parse.</summary>
public static class ResumeEndpoints
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapResumeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/resumes").WithTags("Resumes").RequireAuthorization();

        group.MapPost("/", UploadAsync).DisableAntiforgery().RequireRateLimiting("llm-heavy");
        group.MapPost("/{id:guid}/reparse", ReparseAsync).RequireRateLimiting("llm-heavy");
        group.MapGet("/", ListAsync).WithBrowserCache(30);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

    private static async Task<Results<Ok<ResumeResponse>, BadRequest<ProblemDetails>>> UploadAsync(
        IFormFile file,
        HttpContext ctx,
        IDocumentParserFactory parserFactory,
        IResumeAnalyzer analyzer,
        IBackgroundJobQueue jobQueue,
        AppDbContext db,
        ILogger<Resume> logger,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return TypedResults.BadRequest(new ProblemDetails { Title = "File is required" });
        if (file.Length > MaxUploadBytes)
            return TypedResults.BadRequest(new ProblemDetails { Title = $"File exceeds {MaxUploadBytes / (1024 * 1024)} MB limit" });
        if (!parserFactory.TryResolve(file.ContentType ?? string.Empty, file.FileName ?? string.Empty, out var parser) || parser is null)
            return TypedResults.BadRequest(new ProblemDetails { Title = $"Unsupported file type: {file.ContentType}" });

        var userId = ctx.User.GetUserId();

        ParsedDocument parsed;
        try
        {
            await using var stream = file.OpenReadStream();
            parsed = await parser.ParseAsync(stream, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse resume {FileName}", file.FileName);
            return TypedResults.BadRequest(new ProblemDetails { Title = "Could not extract text from the document", Detail = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(parsed.Text))
            return TypedResults.BadRequest(new ProblemDetails { Title = "Extracted text is empty — is the PDF a scanned image?" });

        var resume = new Resume
        {
            UserId = userId,
            FileName = file.FileName ?? "resume",
            RawText = parsed.Text,
            Status = ProcessingStatus.Summarizing,
        };
        db.Resumes.Add(resume);
        await db.SaveChangesAsync(cancellationToken);

        await AnalyzeAndUpdateAsync(resume, analyzer, jobQueue, db, logger, cancellationToken);
        return TypedResults.Ok(ResumeResponse.From(resume));
    }

    private static async Task<Results<Ok<ResumeResponse>, NotFound<ProblemDetails>>> ReparseAsync(
        Guid id,
        HttpContext ctx,
        IResumeAnalyzer analyzer,
        IBackgroundJobQueue jobQueue,
        AppDbContext db,
        ILogger<Resume> logger,
        CancellationToken cancellationToken)
    {
        var userId = ctx.User.GetUserId();
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);
        if (resume is null)
            return TypedResults.NotFound(new ProblemDetails { Title = "Resume not found" });

        await AnalyzeAndUpdateAsync(resume, analyzer, jobQueue, db, logger, cancellationToken);
        return TypedResults.Ok(ResumeResponse.From(resume));
    }

    /// <summary>
    /// Runs the LLM parse synchronously (the caller awaits the structured profile) and
    /// enqueues the embedding step for background processing. The matcher transparently
    /// recomputes embeddings on demand when the cached value is unavailable, so cache
    /// population is non-blocking and non-critical to the upload response.
    /// </summary>
    private static async Task AnalyzeAndUpdateAsync(
        Resume resume,
        IResumeAnalyzer analyzer,
        IBackgroundJobQueue jobQueue,
        AppDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        resume.Status = ProcessingStatus.Summarizing;
        resume.ErrorMessage = null;

        try
        {
            var profile = await analyzer.AnalyzeAsync(resume.RawText, cancellationToken);
            resume.Summary = profile.Summary;
            resume.StructuredJson = JsonSerializer.Serialize(profile, JsonOpts);
            resume.Status = ProcessingStatus.Done;
            resume.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(resume.Summary))
            {
                var resumeId = resume.Id;
                var summary = resume.Summary;
                await jobQueue.EnqueueAsync((sp, ct) => EmbedResumeSummaryAsync(sp, resumeId, summary, ct), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Resume analysis failed for {ResumeId}", resume.Id);
            resume.Status = ProcessingStatus.Error;
            resume.ErrorMessage = ex.Message;
            resume.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EmbedResumeSummaryAsync(
        IServiceProvider sp, Guid resumeId, string summary, CancellationToken ct)
    {
        var bgDb = sp.GetRequiredService<AppDbContext>();
        var embeddingFactory = sp.GetRequiredService<IEmbeddingProviderFactory>();
        var bgLogger = sp.GetRequiredService<ILogger<Resume>>();
        try
        {
            var emb = await embeddingFactory.GetDefault().EmbedAsync(summary, ct);
            var resume = await bgDb.Resumes.FirstOrDefaultAsync(r => r.Id == resumeId, ct);
            if (resume is null) return;
            resume.SummaryEmbedding = new Vector(emb);
            await bgDb.SaveChangesAsync(ct);
            bgLogger.LogInformation("Cached resume summary embedding for {ResumeId}", resumeId);
        }
        catch (Exception ex)
        {
            bgLogger.LogWarning(ex, "Failed to cache resume summary embedding for {ResumeId}", resumeId);
        }
    }

    private static async Task<Ok<List<ResumeListItem>>> ListAsync(
        HttpContext ctx,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = ctx.User.GetUserId();
        var rows = await db.Resumes
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ResumeListItem(r.Id, r.FileName, r.Status, r.CreatedAt, r.UpdatedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(rows);
    }

    private static async Task<Results<Ok<ResumeResponse>, NotFound>> GetByIdAsync(
        Guid id,
        HttpContext ctx,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = ctx.User.GetUserId();
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);
        return resume is null ? TypedResults.NotFound() : TypedResults.Ok(ResumeResponse.From(resume));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id,
        HttpContext ctx,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = ctx.User.GetUserId();
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);
        if (resume is null) return TypedResults.NotFound();
        db.Resumes.Remove(resume);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    public sealed record ResumeListItem(Guid Id, string FileName, ProcessingStatus Status, DateTime CreatedAt, DateTime UpdatedAt);

    public sealed record ResumeResponse(
        Guid Id,
        string FileName,
        ProcessingStatus Status,
        string? Summary,
        JsonElement? Profile,
        string? ErrorMessage,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public static ResumeResponse From(Resume r) => new(
            r.Id,
            r.FileName,
            r.Status,
            r.Summary,
            r.StructuredJson is null ? null : JsonSerializer.Deserialize<JsonElement>(r.StructuredJson),
            r.ErrorMessage,
            r.CreatedAt,
            r.UpdatedAt);
    }
}
