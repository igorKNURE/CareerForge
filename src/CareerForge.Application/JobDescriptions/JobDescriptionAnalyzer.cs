using System.Text.Json;
using System.Text.Json.Nodes;
using CareerForge.Application.Abstractions.JobDescriptions;
using CareerForge.Application.Abstractions.Llm;
using CareerForge.Application.JobDescriptions.Models;
using Microsoft.Extensions.Logging;

namespace CareerForge.Application.JobDescriptions;

/// <summary>
/// LLM-driven JD parser. Calls providers in tier order and returns the first usable
/// structured profile, throwing if every tier fails.
/// </summary>
public sealed class JobDescriptionAnalyzer(
    ILlmProviderFactory llmFactory,
    ILogger<JobDescriptionAnalyzer> logger)
    : IJobDescriptionAnalyzer
{
    private static readonly (string Provider, string Model)[] Tiers =
    {
        ("gemini", "gemini-2.5-flash"),
        ("groq", "llama-3.3-70b-versatile"),
    };

    private const string SystemPrompt = """
        You are a precise job description parser. Extract a structured profile of a vacancy.
        Distinguish must-have requirements from nice-to-have. Be faithful to the JD text — do not invent.
        Seniority must be one of: junior, mid, senior, staff, principal, lead. Omit if unclear.
        For yearsRequired, infer the minimum years stated; omit if not specified.

        Skill extraction rules — CRITICAL:
        - Each skill entry MUST be an ATOMIC name: a single technology, framework, language, or methodology.
        - SPLIT compound or "or" phrases into separate entries:
          * "SQL Server or other relational databases" → "SQL Server", "PostgreSQL", "MySQL"
          * "Azure or AWS cloud environments" → "Azure", "AWS"
          * ".NET ecosystem (.NET 6 or later)" → ".NET", ".NET 6"
          * "ASP.NET Core for REST API development" → "ASP.NET Core", "REST APIs"
          * "Entity Framework or Dapper" → "Entity Framework", "Dapper"
        - DO NOT include filler words: drop "experience with", "knowledge of", "principles", "practices",
          "ecosystem", "for ... development". Use "SOLID" not "SOLID principles", "DevOps" not "DevOps practices".
        - Each entry should be ≤ 3 words, ideally 1-2.
        """;

    public async Task<JobProfile> AnalyzeAsync(string rawText, string? titleHint = null, CancellationToken cancellationToken = default)
    {
        var schema = BuildSchema();
        var hint = string.IsNullOrWhiteSpace(titleHint) ? string.Empty : $"\n(User-supplied title hint: {titleHint})";
        var userPrompt = $"Job description text:{hint}\n\n{rawText}";

        for (var i = 0; i < Tiers.Length; i++)
        {
            var (provider, model) = Tiers[i];
            var profile = await TryAnalyzeAsync(provider, model, userPrompt, schema, cancellationToken);
            if (profile is not null)
            {
                if (i > 0) logger.LogInformation("JD analysis succeeded on tier {Tier} ({Provider}/{Model})", i + 1, provider, model);
                return Coerce(profile);
            }
        }

        throw new InvalidOperationException("All LLM tiers failed to parse the job description.");
    }

    private async Task<JobProfile?> TryAnalyzeAsync(
        string providerName, string model, string userPrompt, JsonNode schema, CancellationToken ct)
    {
        try
        {
            var llm = llmFactory.Get(providerName);
            var response = await llm.CompleteAsync(new LlmCompletionRequest(
                Model: model,
                SystemPrompt: SystemPrompt,
                Messages: new[] { new LlmMessage(LlmRole.User, userPrompt) },
                Temperature: 0.1,
                MaxOutputTokens: 4096,
                JsonSchema: schema), ct);

            var profile = JsonSerializer.Deserialize<JobProfile>(response.Content, JsonOpts);
            if (profile is null || string.IsNullOrWhiteSpace(profile.Title))
                return null;
            return profile;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "JD analysis on {Provider}/{Model} failed", providerName, model);
            return null;
        }
    }

    private static JobProfile Coerce(JobProfile profile) => profile with
    {
        MustHaveSkills = profile.MustHaveSkills ?? Array.Empty<string>(),
        NiceToHaveSkills = profile.NiceToHaveSkills ?? Array.Empty<string>(),
        Responsibilities = profile.Responsibilities ?? Array.Empty<string>(),
        Qualifications = profile.Qualifications ?? Array.Empty<string>(),
    };

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static JsonNode BuildSchema()
    {
        return new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["title"] = StringProp("Job title — extract from JD or use the hint if provided"),
                ["company"] = StringProp("Company / employer name; empty string if not present"),
                ["seniority"] = StringProp("One of: junior, mid, senior, staff, principal, lead. Empty if unclear."),
                ["yearsRequired"] = new JsonObject { ["type"] = "NUMBER" },
                ["summary"] = StringProp("2-4 sentence summary of the role and what the company is looking for"),
                ["mustHaveSkills"] = StringArray("Required skills, technologies, frameworks"),
                ["niceToHaveSkills"] = StringArray("Preferred but not required skills"),
                ["responsibilities"] = StringArray("Day-to-day duties and ownership areas"),
                ["qualifications"] = StringArray("Education, certifications, formal requirements"),
            },
            ["required"] = new JsonArray("title", "summary", "mustHaveSkills", "niceToHaveSkills", "responsibilities", "qualifications"),
        };
    }

    private static JsonObject StringProp(string? description = null)
    {
        var node = new JsonObject { ["type"] = "STRING" };
        if (description is not null) node["description"] = description;
        return node;
    }

    private static JsonObject StringArray(string? description = null)
    {
        var node = new JsonObject
        {
            ["type"] = "ARRAY",
            ["items"] = new JsonObject { ["type"] = "STRING" },
        };
        if (description is not null) node["description"] = description;
        return node;
    }
}
