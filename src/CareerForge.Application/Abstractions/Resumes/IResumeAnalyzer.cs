using CareerForge.Application.Resumes.Models;

namespace CareerForge.Application.Abstractions.Resumes;

/// <summary>Extracts a structured <see cref="ResumeProfile"/> (skills, roles, summary) from raw resume text.</summary>
public interface IResumeAnalyzer
{
    Task<ResumeProfile> AnalyzeAsync(string rawText, CancellationToken cancellationToken = default);
}
