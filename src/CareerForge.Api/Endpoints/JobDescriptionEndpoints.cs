using System.Text.Json;
using CareerForge.Api.Common;
using CareerForge.Api.Validation;
using CareerForge.Application.Abstractions.Documents;
using CareerForge.Application.Abstractions.JobDescriptions;
using CareerForge.Application.Abstractions.Llm;
using CareerForge.Domain.Entities;
using CareerForge.Domain.Enums;
using CareerForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace CareerForge.Api.Endpoints;

/// <summary>Endpoints for uploading and managing job descriptions (vacancies).</summary>
public static class JobDescriptionEndpoints
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private const int MinTextLength = 80;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapJobDescriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/vacancies").WithTags("Vacancies").RequireAuthorization();

        group.MapPost("/", CreateAsync).WithValidation<CreateVacancyRequest>().RequireRateLimiting("llm-heavy");
        group.MapPost("/upload", UploadAsync).DisableAntiforgery().RequireRateLimiting("llm-heavy");
        group.MapPost("/{id:guid}/reparse", ReparseAsync).RequireRateLimiting("llm-heavy");
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

    public sealed record CreateVacancyRequest(string RawText, string? TitleHint = null);

    private static async Task<Results<Ok<VacancyResponse>, BadRequest<ProblemDetails>>> CreateAsync(
        CreateVacancyRequest request,
        HttpContext ctx,
        IJobDescriptionAnalyzer analyzer,
        IEmbeddingProviderFactory embeddingFactory,
        AppDbContext db,
        ILogger<JobDescription> logger,
        CancellationToken cancellationToken)
    {
        var userId = ctx.User.GetUserId();
        return await PersistAndAnalyzeAsync(request.RawText, request.TitleHint, userId, analyzer, embeddingFactory, db, logger, cancellationToken);
    }

    private static async Task<Results<Ok<VacancyResponse>, BadRequest<ProblemDetails>>> UploadAsync(
        IFormFile file,
        HttpContext ctx,
        IDocumentParserFactory parserFactory,
        IJobDescriptionAnalyzer analyzer,
        IEmbeddingProviderFactory embeddingFactory,
        AppDbContext db,
        ILogger<JobDescription> logger,
        [FromForm] string? titleHint,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return TypedResults.BadRequest(new ProblemDetails { Title = "File is required" });
        if (file.Length > MaxUploadBytes)
            return TypedResults.BadRequest(new ProblemDetails { Title = $"File exceeds {MaxUploadBytes / (1024 * 1024)} MB limit" });
        if (!parserFactory.TryResolve(file.ContentType ?? string.Empty, file.FileName ?? string.Empty, out var parser) || parser is null)
            return TypedResults.BadRequest(new ProblemDetails { Title = $"Unsupported file type: {file.ContentType}" });

        ParsedDocument parsed;
        try
        {
            await using var stream = file.OpenReadStream();
            parsed = await parser.ParseAsync(stream, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse JD file {FileName}", file.FileName);
            return TypedResults.BadRequest(new ProblemDetails { Title = "Could not extract text from the document", Detail = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(parsed.Text) || parsed.Text.Length < MinTextLength)
            return TypedResults.BadRequest(new ProblemDetails { Title = "Extracted text is empty or too short" });

        var userId = ctx.User.GetUserId();
        return await PersistAndAnalyzeAsync(parsed.Text, titleHint, userId, analyzer, embeddingFactory, db, logger, cancellationToken);
    }

    private static async Task<Results<Ok<VacancyResponse>, BadRequest<ProblemDetails>>> PersistAndAnalyzeAsync(
        string rawText,
        string? titleHint,
        Guid userId,
        IJobDescriptionAnalyzer analyzer,
        IEmbeddingProviderFactory embeddingFactory,
        AppDbContext db,
        ILogger<JobDescription> logger,
        CancellationToken cancellationToken)
    {
        var vacancy = new JobDescription
        {
            UserId = userId,
            Title = string.IsNullOrWhiteSpace(titleHint) ? "(pending)" : titleHint,
            RawText = rawText,
            Status = ProcessingStatus.Summarizing,
        };
        db.JobDescriptions.Add(vacancy);
        await db.SaveChangesAsync(cancellationToken);

        await AnalyzeAndUpdateAsync(vacancy, analyzer, embeddingFactory, db, logger, cancellationToken);
        return TypedResults.Ok(VacancyResponse.From(vacancy));
    }

    private static async Task<Results<Ok<VacancyResponse>, NotFound<ProblemDetails>>> ReparseAsync(
        Guid id,
        HttpContext ctx,
        IJobDescriptionAnalyzer analyzer,
        IEmbeddingProviderFactory embeddingFactory,
        AppDbContext db,
        ILogger<JobDescription> logger,
        CancellationToken cancellationToken)
    {
        var userId = ctx.User.GetUserId();
        var vacancy = await db.JobDescriptions.FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, cancellationToken);
        if (vacancy is null)
            return TypedResults.NotFound(new ProblemDetails { Title = "Vacancy not found" });

        await AnalyzeAndUpdateAsync(vacancy, analyzer, embeddingFactory, db, logger, cancellationToken);
        return TypedResults.Ok(VacancyResponse.From(vacancy));
    }

    private static async Task AnalyzeAndUpdateAsync(
        JobDescription vacancy,
        IJobDescriptionAnalyzer analyzer,
        IEmbeddingProviderFactory embeddingFactory,
        AppDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        vacancy.Status = ProcessingStatus.Summarizing;
        vacancy.ErrorMessage = null;

        try
        {
            var profile = await analyzer.AnalyzeAsync(vacancy.RawText, vacancy.Title, cancellationToken);
            vacancy.Title = string.IsNullOrWhiteSpace(profile.Title) ? vacancy.Title : profile.Title;
            vacancy.Company = profile.Company;
            vacancy.Summary = profile.Summary;
            vacancy.StructuredJson = JsonSerializer.Serialize(profile, JsonOpts);
            vacancy.Status = ProcessingStatus.Done;

            if (!string.IsNullOrWhiteSpace(vacancy.Summary))
            {
                try
                {
                    var emb = await embeddingFactory.GetDefault().EmbedAsync(vacancy.Summary, cancellationToken);
                    vacancy.SummaryEmbedding = new Vector(emb);
                }
                catch (Exception embEx)
                {
                    logger.LogWarning(embEx, "Failed to cache summary embedding for {VacancyId}", vacancy.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "JD analysis failed for {VacancyId}", vacancy.Id);
            vacancy.Status = ProcessingStatus.Error;
            vacancy.ErrorMessage = ex.Message;
        }
        finally
        {
            vacancy.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<Ok<List<VacancyListItem>>> ListAsync(
        HttpContext ctx,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = ctx.User.GetUserId();
        var rows = await db.JobDescriptions
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new VacancyListItem(j.Id, j.Title, j.Company, j.Status, j.CreatedAt, j.UpdatedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(rows);
    }

    private static async Task<Results<Ok<VacancyResponse>, NotFound>> GetByIdAsync(
        Guid id,
        HttpContext ctx,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = ctx.User.GetUserId();
        var vacancy = await db.JobDescriptions.FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, cancellationToken);
        return vacancy is null ? TypedResults.NotFound() : TypedResults.Ok(VacancyResponse.From(vacancy));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id,
        HttpContext ctx,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = ctx.User.GetUserId();
        var vacancy = await db.JobDescriptions.FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, cancellationToken);
        if (vacancy is null) return TypedResults.NotFound();
        db.JobDescriptions.Remove(vacancy);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    public sealed record VacancyListItem(Guid Id, string Title, string? Company, ProcessingStatus Status, DateTime CreatedAt, DateTime UpdatedAt);

    public sealed record VacancyResponse(
        Guid Id,
        string Title,
        string? Company,
        ProcessingStatus Status,
        string? Summary,
        JsonElement? Profile,
        string? ErrorMessage,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public static VacancyResponse From(JobDescription j) => new(
            j.Id,
            j.Title,
            j.Company,
            j.Status,
            j.Summary,
            j.StructuredJson is null ? null : JsonSerializer.Deserialize<JsonElement>(j.StructuredJson),
            j.ErrorMessage,
            j.CreatedAt,
            j.UpdatedAt);
    }
}
