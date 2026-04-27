using CareerForge.Domain.Enums;

namespace CareerForge.Application.Abstractions.Interview;

/// <summary>
/// Picks a curated question from the bank that best matches the requested difficulty,
/// preferred categories, and JD skill tags, avoiding anything already asked.
/// Returns <c>null</c> if no suitable question exists.
/// </summary>
public interface IQuestionBankSelector
{
    Task<BankedQuestion?> PickAsync(
        QuestionDifficulty difficulty,
        IReadOnlyList<QuestionCategory> preferredCategories,
        IReadOnlyList<string> jdSkillTags,
        IReadOnlyCollection<string> previouslyAskedTexts,
        string language = "en",
        CancellationToken cancellationToken = default);
}

/// <summary>A bank pick with the count of JD skills it covered (used for ranking).</summary>
public sealed record BankedQuestion(
    Guid Id,
    string Text,
    QuestionCategory Category,
    QuestionDifficulty Difficulty,
    AnswerFormat ExpectedFormat,
    int MatchedSkillCount);
