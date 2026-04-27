using CareerForge.Domain.Enums;
using CareerForge.Domain.ValueObjects;

namespace CareerForge.Domain.Entities;

/// <summary>
/// A single question/answer/evaluation triple inside an <see cref="InterviewSession"/>.
/// </summary>
public class InterviewTurn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int TurnIndex { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public QuestionCategory Category { get; set; }
    public QuestionDifficulty Difficulty { get; set; }
    public AnswerFormat ExpectedFormat { get; set; }
    public string? AnswerText { get; set; }
    public AnswerEvaluation? Evaluation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnsweredAt { get; set; }
}
