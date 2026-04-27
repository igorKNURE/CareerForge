using System.Text.Json;
using CareerForge.Api.Common;
using CareerForge.Api.Validation;
using CareerForge.Application.Abstractions.Interview;
using CareerForge.Application.Abstractions.Llm;
using CareerForge.Application.JobDescriptions.Models;
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

/// <summary>Endpoints for creating interview sessions and stepping through their question / answer turns.</summary>
public static class InterviewEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapInterviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sessions").WithTags("Interview").RequireAuthorization();

        group.MapPost("/", CreateAsync).WithValidation<CreateSessionRequest>();
        group.MapGet("/", ListAsync).WithBrowserCache(30);
        group.MapGet("/{id:guid}", GetByIdAsync).WithBrowserCache(15);
        group.MapPatch("/{id:guid}", RenameAsync).WithValidation<RenameSessionRequest>();
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPost("/{id:guid}/turns", GenerateNextQuestionAsync).RequireRateLimiting("llm-interactive");
        group.MapPost("/{id:guid}/turns/{turnId:guid}/answer", SubmitAnswerAsync)
            .WithValidation<SubmitAnswerRequest>()
            .RequireRateLimiting("llm-interactive");

        return app;
    }

    public sealed record CreateSessionRequest(Guid ResumeId, Guid JobDescriptionId, string? Name = null, string? Language = null);

    private static async Task<Results<Ok<SessionResponse>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> CreateAsync(
        CreateSessionRequest request,
        HttpContext ctx,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.Id == request.ResumeId && r.UserId == userId, ct);
        if (resume is null) return TypedResults.NotFound(new ProblemDetails { Title = "Resume not found" });
        if (string.IsNullOrWhiteSpace(resume.StructuredJson))
            return TypedResults.BadRequest(new ProblemDetails { Title = "Resume has not been analysed yet" });

        var vacancy = await db.JobDescriptions.FirstOrDefaultAsync(j => j.Id == request.JobDescriptionId && j.UserId == userId, ct);
        if (vacancy is null) return TypedResults.NotFound(new ProblemDetails { Title = "Vacancy not found" });
        if (string.IsNullOrWhiteSpace(vacancy.StructuredJson))
            return TypedResults.BadRequest(new ProblemDetails { Title = "Vacancy has not been analysed yet" });

        var session = new InterviewSession
        {
            UserId = userId,
            ResumeId = resume.Id,
            JobDescriptionId = vacancy.Id,
            Name = string.IsNullOrWhiteSpace(request.Name) ? $"{vacancy.Title} prep" : request.Name,
            Language = CareerForge.Application.Abstractions.Llm.LanguageInstruction.Normalize(request.Language),
        };
        db.InterviewSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(SessionResponse.From(session, Array.Empty<InterviewTurn>()));
    }

    private static async Task<Ok<List<SessionListItem>>> ListAsync(HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var rows = await db.InterviewSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SessionListItem(s.Id, s.Name, s.Language, s.ResumeId, s.JobDescriptionId, s.TurnCount, s.CreatedAt, s.UpdatedAt))
            .ToListAsync(ct);
        return TypedResults.Ok(rows);
    }

    private static async Task<Results<Ok<SessionResponse>, NotFound>> GetByIdAsync(
        Guid id, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var session = await db.InterviewSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
        if (session is null) return TypedResults.NotFound();
        var turns = await db.InterviewTurns
            .Where(t => t.SessionId == id)
            .OrderBy(t => t.TurnIndex)
            .ToListAsync(ct);
        return TypedResults.Ok(SessionResponse.From(session, turns));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var session = await db.InterviewSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
        if (session is null) return TypedResults.NotFound();
        db.InterviewSessions.Remove(session);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    public sealed record RenameSessionRequest(string Name);

    private static async Task<Results<Ok<SessionListItem>, NotFound>> RenameAsync(
        Guid id, RenameSessionRequest request, HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var session = await db.InterviewSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
        if (session is null) return TypedResults.NotFound();
        session.Name = request.Name.Trim();
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new SessionListItem(
            session.Id, session.Name, session.Language, session.ResumeId, session.JobDescriptionId,
            session.TurnCount, session.CreatedAt, session.UpdatedAt));
    }

    private static async Task<Results<Ok<TurnResponse>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> GenerateNextQuestionAsync(
        Guid id,
        HttpContext ctx,
        AppDbContext db,
        IQuestionGenerator generator,
        ILogger<InterviewSession> logger,
        CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var session = await db.InterviewSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
        if (session is null) return TypedResults.NotFound(new ProblemDetails { Title = "Session not found" });

        var (resumeProfile, vacancyProfile, error) = await LoadProfilesAsync(session, db, ct);
        if (error is not null) return TypedResults.BadRequest(error);

        var existingTurns = await db.InterviewTurns
            .Where(t => t.SessionId == id)
            .OrderBy(t => t.TurnIndex)
            .Select(t => new PreviousTurn(
                t.TurnIndex,
                t.QuestionText,
                t.Category.ToString(),
                t.Evaluation == null ? (int?)null : t.Evaluation.OverallScore,
                t.Evaluation != null && t.Evaluation.EvaluationFailed,
                t.Difficulty.ToString()))
            .ToListAsync(ct);

        var generated = await generator.GenerateAsync(resumeProfile!, vacancyProfile!, existingTurns, session.Language, ct);

        var nextIndex = existingTurns.Count == 0 ? 0 : existingTurns.Max(t => t.Index) + 1;
        var turn = new InterviewTurn
        {
            SessionId = id,
            TurnIndex = nextIndex,
            QuestionText = generated.Text,
            Category = generated.Category,
            Difficulty = generated.Difficulty,
            ExpectedFormat = generated.ExpectedFormat,
        };
        db.InterviewTurns.Add(turn);
        session.TurnCount = nextIndex + 1;
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Session {SessionId} turn {Index}: generated {Category}/{Difficulty}",
            id, nextIndex, generated.Category, generated.Difficulty);

        return TypedResults.Ok(TurnResponse.From(turn, generated.Rationale));
    }

    public sealed record SubmitAnswerRequest(string AnswerText);

    private static async Task<Results<Ok<TurnResponse>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> SubmitAnswerAsync(
        Guid id,
        Guid turnId,
        SubmitAnswerRequest request,
        HttpContext ctx,
        AppDbContext db,
        IAnswerEvaluator evaluator,
        ILogger<InterviewTurn> logger,
        CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var session = await db.InterviewSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
        if (session is null) return TypedResults.NotFound(new ProblemDetails { Title = "Session not found" });

        var turn = await db.InterviewTurns.FirstOrDefaultAsync(t => t.Id == turnId && t.SessionId == id, ct);
        if (turn is null) return TypedResults.NotFound(new ProblemDetails { Title = "Turn not found" });

        var (resumeProfile, vacancyProfile, error) = await LoadProfilesAsync(session, db, ct);
        if (error is not null) return TypedResults.BadRequest(error);

        var evaluation = await evaluator.EvaluateAsync(
            resumeProfile!, vacancyProfile!,
            turn.QuestionText, turn.Category, turn.ExpectedFormat,
            request.AnswerText, session.Language, ct);

        turn.AnswerText = request.AnswerText;
        turn.Evaluation = evaluation;
        turn.AnsweredAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Turn {TurnId} evaluated: overall={Score}", turnId, evaluation.OverallScore);

        return TypedResults.Ok(TurnResponse.From(turn, null));
    }


    private static async Task<(ResumeProfile?, JobProfile?, ProblemDetails?)> LoadProfilesAsync(
        InterviewSession session, AppDbContext db, CancellationToken ct)
    {
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.Id == session.ResumeId, ct);
        var vacancy = await db.JobDescriptions.FirstOrDefaultAsync(j => j.Id == session.JobDescriptionId, ct);
        if (resume?.StructuredJson is null || vacancy?.StructuredJson is null)
            return (null, null, new ProblemDetails { Title = "Linked resume or vacancy is missing structured profile" });

        try
        {
            return (
                JsonSerializer.Deserialize<ResumeProfile>(resume.StructuredJson, JsonOpts),
                JsonSerializer.Deserialize<JobProfile>(vacancy.StructuredJson, JsonOpts),
                null);
        }
        catch (JsonException ex)
        {
            return (null, null, new ProblemDetails { Title = "Stored profile JSON is malformed", Detail = ex.Message });
        }
    }

    public sealed record SessionListItem(Guid Id, string Name, string Language, Guid ResumeId, Guid JobDescriptionId, int TurnCount, DateTime CreatedAt, DateTime UpdatedAt);

    public sealed record SessionResponse(
        Guid Id, string Name, string Language, Guid ResumeId, Guid JobDescriptionId, int TurnCount,
        DateTime CreatedAt, DateTime UpdatedAt,
        IReadOnlyList<TurnResponse> Turns)
    {
        public static SessionResponse From(InterviewSession s, IReadOnlyList<InterviewTurn> turns)
            => new(s.Id, s.Name, s.Language, s.ResumeId, s.JobDescriptionId, s.TurnCount, s.CreatedAt, s.UpdatedAt,
                turns.Select(t => TurnResponse.From(t, null)).ToList());
    }

    public sealed record TurnResponse(
        Guid Id, int TurnIndex, string QuestionText,
        QuestionCategory Category, QuestionDifficulty Difficulty, AnswerFormat ExpectedFormat,
        string? Rationale, string? AnswerText, AnswerEvaluation? Evaluation,
        DateTime CreatedAt, DateTime? AnsweredAt)
    {
        public static TurnResponse From(InterviewTurn t, string? rationale)
            => new(t.Id, t.TurnIndex, t.QuestionText, t.Category, t.Difficulty, t.ExpectedFormat,
                rationale, t.AnswerText, t.Evaluation, t.CreatedAt, t.AnsweredAt);
    }
}
