using CareerForge.Application.JobDescriptions.Models;
using CareerForge.Application.Matching.Models;
using CareerForge.Application.Resumes.Models;

namespace CareerForge.Application.Abstractions.Matching;

/// <summary>
/// Computes the overall match score from skill coverage, semantic similarity,
/// and experience fit using a weighted average.
/// </summary>
public interface IMatchScorer
{
    /// <summary>
    /// Score the given resume against the given vacancy. Cached embeddings, when supplied,
    /// skip a fresh embedding call and short-circuit the semantic-similarity step.
    /// </summary>
    Task<MatchScoringResult> ScoreAsync(
        ResumeProfile resume,
        JobProfile vacancy,
        float[]? cachedResumeEmbedding = null,
        float[]? cachedVacancyEmbedding = null,
        string language = "en",
        CancellationToken cancellationToken = default);
}
