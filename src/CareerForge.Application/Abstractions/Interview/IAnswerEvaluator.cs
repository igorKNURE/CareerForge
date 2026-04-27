using CareerForge.Application.JobDescriptions.Models;
using CareerForge.Application.Resumes.Models;
using CareerForge.Domain.Enums;
using CareerForge.Domain.ValueObjects;

namespace CareerForge.Application.Abstractions.Interview;

/// <summary>
/// Scores a candidate's answer against rubric dimensions (content, structure, relevance)
/// and returns qualitative strengths / weaknesses / recommendations.
/// </summary>
public interface IAnswerEvaluator
{
    Task<AnswerEvaluation> EvaluateAsync(
        ResumeProfile resume,
        JobProfile vacancy,
        string questionText,
        QuestionCategory category,
        AnswerFormat expectedFormat,
        string answerText,
        string language = "en",
        CancellationToken cancellationToken = default);
}
