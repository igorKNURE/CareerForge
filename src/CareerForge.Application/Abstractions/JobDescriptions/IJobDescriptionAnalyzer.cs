using CareerForge.Application.JobDescriptions.Models;

namespace CareerForge.Application.Abstractions.JobDescriptions;

/// <summary>Extracts a structured <see cref="JobProfile"/> (must-have / nice-to-have skills, seniority, etc.) from raw JD text.</summary>
public interface IJobDescriptionAnalyzer
{
    Task<JobProfile> AnalyzeAsync(string rawText, string? titleHint = null, CancellationToken cancellationToken = default);
}
