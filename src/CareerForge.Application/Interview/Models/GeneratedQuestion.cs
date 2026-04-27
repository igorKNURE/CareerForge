using CareerForge.Domain.Enums;

namespace CareerForge.Application.Interview.Models;

/// <summary>A freshly produced interview question with its category, difficulty, expected format, and rationale.</summary>
public sealed record GeneratedQuestion(
    string Text,
    QuestionCategory Category,
    QuestionDifficulty Difficulty,
    AnswerFormat ExpectedFormat,
    string Rationale);
