using CareerForge.Application.Abstractions.Interview;
using CareerForge.Domain.Enums;
using CareerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerForge.Infrastructure.Interview;

/// <summary>
/// Picks a curated bank question by JD-skill-tag overlap, breaking ties by least-used so we
/// rotate through the catalogue. The bank is English-only; non-English requests bypass it.
/// </summary>
public sealed class QuestionBankSelector(AppDbContext db) : IQuestionBankSelector
{
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
            })
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
