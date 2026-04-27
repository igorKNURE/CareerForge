using CareerForge.Domain.Enums;

namespace CareerForge.Domain.ValueObjects;

/// <summary>
/// A single observation produced by the matching pipeline (gap, risk, or note),
/// with an optional recommendation and a resume excerpt when applicable.
/// </summary>
public sealed record MatchFinding(
    FindingSeverity Severity,
    string Category,
    string Title,
    string Description,
    string? Recommendation,
    string? ResumeExcerpt);
