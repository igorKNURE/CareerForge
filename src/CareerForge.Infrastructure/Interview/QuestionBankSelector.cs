using CareerForge.Application.Abstractions.Interview;
using CareerForge.Domain.Enums;
using CareerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerForge.Infrastructure.Interview;

/// <summary>
/// Picks a curated bank question that is genuinely relevant to the role. An entry is
/// considered relevant when at least one of its skill tags overlaps the job description,
/// or when it is tagged as <c>general</c> (role-agnostic warm-ups such as
/// behavioural / resume questions). Entries without overlap and without the
/// <c>general</c> tag are excluded so a 3D artist applying for a non-engineering role
/// is never offered a .NET question.
/// </summary>
public sealed class QuestionBankSelector(AppDbContext db) : IQuestionBankSelector
{
    private const string GeneralTag = "general";

    public async Task<BankedQuestion?> PickAsync(
        QuestionDifficulty difficulty,
        IReadOnlyList<QuestionCategory> preferredCategories,
        IReadOnlyList<string> jdSkillTags,
        IReadOnlyCollection<string> previouslyAskedTexts,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
            return null;

        var jdSkills = jdSkillTags.Select(NormalizeTag).Where(s => s.Length > 0).ToHashSet();
        var askedNorm = previouslyAskedTexts.Select(NormalizeText).ToHashSet();

        var pool = await db.QuestionBank
            .Where(q => q.Difficulty == difficulty)
            .Where(q => preferredCategories.Contains(q.Category))
            .ToListAsync(cancellationToken);

        var ranked = pool
            .Where(q => !askedNorm.Contains(NormalizeText(q.Text)))
            .Select(q => new
            {
                Entry = q,
                MatchCount = q.SkillTags.Count(t => jdSkills.Contains(NormalizeTag(t))),
                IsGeneral = q.SkillTags.Any(t => string.Equals(NormalizeTag(t), GeneralTag, StringComparison.Ordinal)),
            })
            .Where(x => x.MatchCount > 0 || x.IsGeneral)
            .OrderByDescending(x => x.MatchCount)
            .ThenBy(x => x.Entry.UseCount)
            .ThenBy(_ => Random.Shared.Next())
            .ToList();

        var pick = ranked.FirstOrDefault();
        if (pick is null) return null;

        pick.Entry.UseCount++;
        await db.SaveChangesAsync(cancellationToken);

        return new BankedQuestion(
            pick.Entry.Id,
            pick.Entry.Text,
            pick.Entry.Category,
            pick.Entry.Difficulty,
            pick.Entry.ExpectedFormat,
            pick.MatchCount);
    }

    private static string NormalizeTag(string tag) =>
        new(tag.Trim().ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '#' || c == '+').ToArray());

    private static string NormalizeText(string text) => text.Trim().ToLowerInvariant();
}
