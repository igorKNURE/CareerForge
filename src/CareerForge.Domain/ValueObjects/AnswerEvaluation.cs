namespace CareerForge.Domain.ValueObjects;

/// <summary>
/// Rubric-style scoring of an interview answer with qualitative feedback.
/// <see cref="EvaluationFailed"/> indicates the LLM call did not produce a usable result.
/// </summary>
public sealed record AnswerEvaluation(
    int ContentScore,
    int StructureScore,
    int RelevanceScore,
    int OverallScore,
    string Strengths,
    string Weaknesses,
    IReadOnlyList<string> Recommendations,
    bool EvaluationFailed = false);
