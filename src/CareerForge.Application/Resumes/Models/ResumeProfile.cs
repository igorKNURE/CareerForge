namespace CareerForge.Application.Resumes.Models;

/// <summary>Structured representation of a resume extracted by <see cref="ResumeAnalyzer"/>.</summary>
public sealed record ResumeProfile(
    string FullName,
    string Headline,
    string Summary,
    double? YearsOfExperience,
    IReadOnlyList<string> Skills,
    IReadOnlyList<ExperienceEntry> Experience,
    IReadOnlyList<EducationEntry> Education);

/// <summary>One job entry inside a <see cref="ResumeProfile"/>.</summary>
public sealed record ExperienceEntry(
    string Company,
    string Role,
    string? StartDate,
    string? EndDate,
    IReadOnlyList<string> Highlights);

/// <summary>One education entry inside a <see cref="ResumeProfile"/>.</summary>
public sealed record EducationEntry(
    string Institution,
    string Degree,
    string? Year);
