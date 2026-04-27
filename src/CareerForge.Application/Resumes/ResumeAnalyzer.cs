using System.Text.Json;
using System.Text.Json.Nodes;
using CareerForge.Application.Abstractions.Llm;
using CareerForge.Application.Abstractions.Resumes;
using CareerForge.Application.Resumes.Models;
using Microsoft.Extensions.Logging;

namespace CareerForge.Application.Resumes;

/// <summary>
/// LLM-driven resume parser. Calls providers in tier order and returns the first usable
/// structured profile, throwing if every tier fails.
/// </summary>
public sealed class ResumeAnalyzer(
    ILlmProviderFactory llmFactory,
    ILogger<ResumeAnalyzer> logger)
    : IResumeAnalyzer
{
    private static readonly (string Provider, string Model)[] Tiers =
    {
        ("gemini", "gemini-2.5-flash"),
        ("groq", "llama-3.3-70b-versatile"),
    };

    private const string SystemPrompt = """
        You are a precise resume parser. Extract a structured profile from the candidate's resume text.
        Be faithful to what's written. Do not invent experience, dates, or skills.
        For yearsOfExperience, estimate from work history if possible; otherwise omit.
        For each experience entry, extract 2-5 highlight bullets verbatim or lightly cleaned.
        Skills should include both technical (languages, tools) and notable professional skills.
        For education, capture EVERY degree, diploma, bootcamp, or notable certification mentioned
        anywhere in the resume — under headings like "Education", "Academic Background", "Studies",
        "Qualifications", "Certifications", or as bullet/inline mentions in the summary, header, or
        contact block. Output one entry per item with institution, degree (or qualification name),
        and year if present. Only emit an empty array if you are confident the resume genuinely
        contains no educational history at all.
        """;

    public async Task<ResumeProfile> AnalyzeAsync(string rawText, CancellationToken cancellationToken = default)
    {
        var schema = BuildSchema();
        var userPrompt = $"Resume text:\n\n{rawText}";

        for (var i = 0; i < Tiers.Length; i++)
        {
            var (provider, model) = Tiers[i];
            var profile = await TryAnalyzeAsync(provider, model, userPrompt, schema, cancellationToken);
            if (profile is not null)
            {
                if (i > 0) logger.LogInformation("Resume analysis succeeded on tier {Tier} ({Provider}/{Model})", i + 1, provider, model);
                return Coerce(profile);
            }
        }

        throw new InvalidOperationException("All LLM tiers failed to parse the resume.");
    }

    private async Task<ResumeProfile?> TryAnalyzeAsync(
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

            var profile = JsonSerializer.Deserialize<ResumeProfile>(response.Content, JsonOpts);
            if (profile is null || string.IsNullOrWhiteSpace(profile.FullName))
                return null;
            return profile;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Resume analysis on {Provider}/{Model} failed", providerName, model);
            return null;
        }
    }

    private static ResumeProfile Coerce(ResumeProfile profile) => profile with
    {
        Skills = profile.Skills ?? Array.Empty<string>(),
        Experience = profile.Experience ?? Array.Empty<ExperienceEntry>(),
        Education = profile.Education ?? Array.Empty<EducationEntry>(),
    };

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static JsonNode BuildSchema()
    {
        return new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["fullName"] = StringProp("Candidate full name; empty string if not present"),
                ["headline"] = StringProp("Current professional headline or most recent job title"),
                ["summary"] = StringProp("2-4 sentence professional summary derived from the resume"),
                ["yearsOfExperience"] = new JsonObject { ["type"] = "NUMBER" },
                ["skills"] = new JsonObject
                {
                    ["type"] = "ARRAY",
                    ["items"] = new JsonObject { ["type"] = "STRING" },
                },
                ["experience"] = new JsonObject
                {
                    ["type"] = "ARRAY",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "OBJECT",
                        ["properties"] = new JsonObject
                        {
                            ["company"] = StringProp(),
                            ["role"] = StringProp(),
                            ["startDate"] = StringProp(),
                            ["endDate"] = StringProp(),
                            ["highlights"] = new JsonObject
                            {
                                ["type"] = "ARRAY",
                                ["items"] = new JsonObject { ["type"] = "STRING" },
                            },
                        },
                    },
                },
                ["education"] = new JsonObject
                {
                    ["type"] = "ARRAY",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "OBJECT",
                        ["properties"] = new JsonObject
                        {
                            ["institution"] = StringProp(),
                            ["degree"] = StringProp(),
                            ["year"] = StringProp(),
                        },
                    },
                },
            },
            ["required"] = new JsonArray("fullName", "headline", "summary", "skills", "experience", "education"),
        };
    }

    private static JsonObject StringProp(string? description = null)
    {
        var node = new JsonObject { ["type"] = "STRING" };
        if (description is not null) node["description"] = description;
        return node;
    }
}
