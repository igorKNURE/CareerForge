using CareerForge.Application.Interview.Models;
using CareerForge.Application.JobDescriptions.Models;
using CareerForge.Application.Resumes.Models;

namespace CareerForge.Application.Abstractions.Interview;

/// <summary>
/// Produces the next interview question tailored to the resume + vacancy and aware of
/// what has already been asked so the conversation doesn't repeat itself.
/// </summary>
public interface IQuestionGenerator
{
    Task<GeneratedQuestion> GenerateAsync(
        ResumeProfile resume,
        JobProfile vacancy,
        IReadOnlyList<PreviousTurn> previousTurns,
        string language = "en",
        CancellationToken cancellationToken = default);
}

/// <summary>Lightweight summary of an earlier turn passed back into question generation for context.</summary>
public sealed record PreviousTurn(int Index, string Question, string Category, int? OverallScore = null, bool EvaluationFailed = false, string? Difficulty = null);
