namespace CareerForge.Application.Abstractions.Llm;

/// <summary>
/// Builds the trailing system-prompt fragment that pins the LLM's output language
/// while leaving enum values and proper-noun technologies in English.
/// </summary>
public static class LanguageInstruction
{
    /// <summary>Returns the language directive to append to a system prompt.</summary>
    public static string Build(string language) => Normalize(language) switch
    {
        "uk" => "\n\nIMPORTANT: Write ALL your output text (questions, descriptions, recommendations, summaries, every prose field) in Ukrainian. Keep proper-noun technologies (e.g. .NET, PostgreSQL, React, IQueryable) untranslated. Enum values, JSON keys, and any field constrained to a fixed enum stay in English exactly as specified.",
        _ => "\n\nWrite all output text in English.",
    };

    /// <summary>Coerces an arbitrary BCP-47-ish tag to one of the supported codes ("en" or "uk").</summary>
    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "en";
        var lower = language.Trim().ToLowerInvariant();
        var dash = lower.IndexOf('-');
        if (dash > 0) lower = lower[..dash];
        return lower switch
        {
            "uk" => "uk",
            _ => "en",
        };
    }
}
