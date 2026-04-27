using CareerForge.Domain.ValueObjects;

namespace CareerForge.Application.Matching.Models;

/// <summary>
/// Output of <see cref="Abstractions.Matching.IMatchScorer"/>: deterministic component scores
/// plus the LLM-generated findings and prose improvement summary.
/// </summary>
public sealed record MatchScoringResult(
    decimal OverallScore,
    decimal SkillCoverageScore,
    decimal SemanticSimilarityScore,
    decimal ExperienceFitScore,
    IReadOnlyList<string> MatchedMustHaveSkills,
    IReadOnlyList<string> MissingMustHaveSkills,
    IReadOnlyList<string> MatchedNiceToHaveSkills,
    IReadOnlyList<MatchFinding> Findings,
    string ImprovementSummary);
