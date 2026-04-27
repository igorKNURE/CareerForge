namespace CareerForge.Application.JobDescriptions.Models;

/// <summary>Structured representation of a vacancy extracted by <see cref="JobDescriptionAnalyzer"/>.</summary>
public sealed record JobProfile(
    string Title,
    string? Company,
    string? Seniority,
    double? YearsRequired,
    string Summary,
    IReadOnlyList<string> MustHaveSkills,
    IReadOnlyList<string> NiceToHaveSkills,
    IReadOnlyList<string> Responsibilities,
    IReadOnlyList<string> Qualifications);
