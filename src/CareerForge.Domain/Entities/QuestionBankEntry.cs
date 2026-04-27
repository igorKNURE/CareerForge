using CareerForge.Domain.Enums;

namespace CareerForge.Domain.Entities;

/// <summary>
/// A reusable curated question used to seed interview sessions when LLM generation isn't suitable.
/// </summary>
public class QuestionBankEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public QuestionCategory Category { get; set; }
    public QuestionDifficulty Difficulty { get; set; }
    public AnswerFormat ExpectedFormat { get; set; }
    public List<string> SkillTags { get; set; } = new();
    public string Source { get; set; } = "seed";
    public int UseCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
