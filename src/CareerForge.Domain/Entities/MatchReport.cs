using CareerForge.Domain.ValueObjects;

namespace CareerForge.Domain.Entities;

/// <summary>
/// Persisted scoring of a resume against a job description, including skill coverage and findings.
/// </summary>
public class MatchReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ResumeId { get; set; }
    public Guid JobDescriptionId { get; set; }
    public decimal OverallScore { get; set; }
    public decimal SkillCoverageScore { get; set; }
    public decimal SemanticSimilarityScore { get; set; }
    public decimal ExperienceFitScore { get; set; }
    public List<string> MatchedMustHaveSkills { get; set; } = new();
    public List<string> MissingMustHaveSkills { get; set; } = new();
    public List<string> MatchedNiceToHaveSkills { get; set; } = new();
    public List<MatchFinding> Findings { get; set; } = new();
    public string? ImprovementSummary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
