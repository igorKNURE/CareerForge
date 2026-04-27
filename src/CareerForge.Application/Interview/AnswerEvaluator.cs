using System.Text.Json;
using System.Text.Json.Nodes;
using CareerForge.Application.Abstractions.Interview;
using CareerForge.Application.Abstractions.Llm;
using CareerForge.Application.JobDescriptions.Models;
using CareerForge.Application.Resumes.Models;
using CareerForge.Domain.Enums;
using CareerForge.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CareerForge.Application.Interview;

/// <summary>
/// LLM-based answer scorer. Each provider tier is tried in order; if all tiers fail
/// it returns an <see cref="AnswerEvaluation"/> with <c>EvaluationFailed = true</c>
/// rather than throwing.
/// </summary>
public sealed class AnswerEvaluator(
    ILlmProviderFactory llmFactory,
    ILogger<AnswerEvaluator> logger)
    : IAnswerEvaluator
{
    private static readonly (string Provider, string Model)[] Tiers =
    {
        ("gemini", "gemini-2.5-flash"),
        ("groq", "llama-3.3-70b-versatile"),
    };

    private const string SystemPrompt = """
        You are a calibrated interview coach grading the answer the candidate just gave.
        Remember they are meant to be SPEAKING this answer in a live interview, not writing a wiki page.

        Voice for all prose (strengths, weaknesses, recommendations): address the candidate DIRECTLY in the SECOND PERSON ("you", "your"). In Ukrainian, use the formal "ви/вам/ваш". NEVER refer to them by name or in the third person ("the candidate", "they", "his/her").

        Score three dimensions on 0–5 (integer):
          - content: technical/factual accuracy and depth. For Resume / Behavioral questions, cross-reference the candidate's claims against the experience entries in the user message — reward answers that accurately surface what's actually there, and flag (in the weakness) any claim that the resume does NOT support. For Technical / Coding questions, judge factual accuracy on its own merits; the resume is just background context.
          - structure: how well-delivered the answer is FOR A SPOKEN INTERVIEW. Specifically:
              * 5 = natural conversational flow with clear organisation; full sentences with connective tissue ("first I'd...", "the trade-off there is...", "in my experience..."); follows the expected format where applicable
              * 3 = correct content but choppy delivery — short sentences, missing connectives, a bit listy
              * 1–2 = reads like dumped study notes / wiki bullets / fragmented headings ("Thread-safe ✓", "Fast and lightweight"). Even if technically correct, this is NOT how you should sound out loud. Call this out explicitly.
              * 0 = no structure at all
          - relevance: does the answer actually address what was asked and connect to the role?

        Do NOT compute or return an overall score — the system computes it deterministically from your three axis scores.

        Be concise:
          - 'strengths': ONE sentence under 200 chars, addressed to you ("You clearly explain…").
          - 'weaknesses': ONE sentence under 200 chars, addressed to you. If delivery sounds like notes/bullets, name it: e.g. "Reads like written study notes — in a real interview you'd talk through this conversationally."
          - 'recommendations': 2–4 bullets, each under 160 chars, phrased as direct advice ("Add a concrete example…", "Slow down and connect the steps…"). Always include at least one delivery/phrasing tip if structure scored ≤ 3.

        Be honest and specific. If the answer is empty or off-topic, score low and say so. Do NOT inflate scores for content alone — a 5/5 content with 1/5 structure is a real and common pattern; surface it.
        """;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LenientEnumConverterFactory() },
    };

    public async Task<AnswerEvaluation> EvaluateAsync(
        ResumeProfile resume,
        JobProfile vacancy,
        string questionText,
        QuestionCategory category,
        AnswerFormat expectedFormat,
        string answerText,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var lang = LanguageInstruction.Normalize(language);
        var schema = BuildSchema();

        var resumeContext = BuildResumeContext(resume, category);
        var roleContext = BuildRoleContext(vacancy, category);

        var prompt = $"""
            ## Target role
            {roleContext}

            ## Candidate background
            {resumeContext}

            ## Interview question
            Category: {category}
            Expected format: {expectedFormat}
            Question: {questionText}

            ## Candidate's answer
            {answerText}

            Evaluate this answer.
            """;

        var systemPrompt = SystemPrompt + LanguageInstruction.Build(lang);

        for (var i = 0; i < Tiers.Length; i++)
        {
            var (provider, model) = Tiers[i];
            var attempt = await TryEvaluateAsync(provider, model, systemPrompt, prompt, schema, cancellationToken);
            if (attempt is not null)
            {
                if (i > 0) logger.LogInformation("Answer eval succeeded on tier {Tier} ({Provider}/{Model})", i + 1, provider, model);
                return attempt;
            }
        }

        logger.LogWarning("All answer-eval tiers failed; returning unavailable");
        return Unavailable(lang == "uk"
            ? "Оцінювання ШІ зараз недоступне (усі провайдери обмежені або недоступні). Спробуйте за хвилину."
            : "AI evaluation unavailable right now (all providers rate-limited or down). Please try again in a minute.");
    }

    private async Task<AnswerEvaluation?> TryEvaluateAsync(
        string providerName, string model, string systemPrompt, string prompt, JsonNode schema, CancellationToken cancellationToken)
    {
        try
        {
            var llm = llmFactory.Get(providerName);
            var response = await llm.CompleteAsync(new LlmCompletionRequest(
                Model: model,
                SystemPrompt: systemPrompt,
                Messages: new[] { new LlmMessage(LlmRole.User, prompt) },
                Temperature: 0.2,
                MaxOutputTokens: 4096,
                JsonSchema: schema), cancellationToken);
            var parsed = JsonSerializer.Deserialize<AnswerEvaluation>(response.Content, JsonOpts);
            if (parsed is null) return null;
            return parsed with
            {
                OverallScore = ComputeOverallScore(parsed.ContentScore, parsed.StructureScore, parsed.RelevanceScore),
                EvaluationFailed = false,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Answer eval on {Provider}/{Model} failed", providerName, model);
            return null;
        }
    }

    /// <summary>
    /// Builds the resume context surfaced to the evaluator. Resume and Behavioral
    /// categories receive the full experience entries (allowing claim verification);
    /// technical categories receive a leaner summary + skills view to limit token cost.
    /// </summary>
    private static string BuildResumeContext(ResumeProfile resume, QuestionCategory category)
    {
        var lean = $"""
            {resume.Summary}
            Skills on file: {string.Join(", ", resume.Skills ?? Array.Empty<string>())}
            """;

        if (category is not (QuestionCategory.Resume or QuestionCategory.Behavioral))
            return lean;

        var experience = resume.Experience ?? Array.Empty<ExperienceEntry>();
        if (experience.Count == 0) return lean;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(lean);
        sb.AppendLine();
        sb.AppendLine("Experience entries (cross-reference any claims in the answer against these):");
        foreach (var e in experience)
        {
            var dates = string.IsNullOrWhiteSpace(e.EndDate) ? e.StartDate : $"{e.StartDate} – {e.EndDate}";
            sb.Append("- ").Append(e.Role).Append(" @ ").Append(e.Company);
            if (!string.IsNullOrWhiteSpace(dates)) sb.Append(" (").Append(dates).Append(')');
            sb.AppendLine();
            if (e.Highlights is not null)
                foreach (var h in e.Highlights)
                    sb.Append("    • ").AppendLine(h);
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds the role context surfaced to the evaluator. SystemDesign questions
    /// additionally receive the must-have skills and responsibilities, enabling
    /// architecture-focused grading against the role's stated requirements.
    /// </summary>
    private static string BuildRoleContext(JobProfile vacancy, QuestionCategory category)
    {
        var lean = $"""
            {vacancy.Title}{(vacancy.Company is null ? "" : " @ " + vacancy.Company)}
            {vacancy.Summary}
            """;

        if (category is not QuestionCategory.SystemDesign) return lean;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(lean);
        if ((vacancy.MustHaveSkills?.Count ?? 0) > 0)
        {
            sb.Append("Must-have skills: ").AppendLine(string.Join(", ", vacancy.MustHaveSkills!));
        }
        if ((vacancy.Responsibilities?.Count ?? 0) > 0)
        {
            sb.AppendLine("Responsibilities:");
            foreach (var r in vacancy.Responsibilities!)
                sb.Append("- ").AppendLine(r);
        }
        return sb.ToString().TrimEnd();
    }

    private static int ComputeOverallScore(int content, int structure, int relevance)
    {
        var c = Math.Clamp(content, 0, 5);
        var s = Math.Clamp(structure, 0, 5);
        var r = Math.Clamp(relevance, 0, 5);
        return (int)Math.Round(c * 10.0 + s * 4.0 + r * 6.0);
    }

    private static AnswerEvaluation Unavailable(string message) => new(
        ContentScore: 0, StructureScore: 0, RelevanceScore: 0, OverallScore: 0,
        Strengths: string.Empty,
        Weaknesses: message,
        Recommendations: Array.Empty<string>(),
        EvaluationFailed: true);

    private static JsonNode BuildSchema()
    {
        return new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["contentScore"] = new JsonObject { ["type"] = "INTEGER", ["description"] = "0–5" },
                ["structureScore"] = new JsonObject { ["type"] = "INTEGER", ["description"] = "0–5" },
                ["relevanceScore"] = new JsonObject { ["type"] = "INTEGER", ["description"] = "0–5" },
                ["strengths"] = new JsonObject { ["type"] = "STRING" },
                ["weaknesses"] = new JsonObject { ["type"] = "STRING" },
                ["recommendations"] = new JsonObject
                {
                    ["type"] = "ARRAY",
                    ["items"] = new JsonObject { ["type"] = "STRING" },
                },
            },
            ["required"] = new JsonArray("contentScore", "structureScore", "relevanceScore", "strengths", "weaknesses", "recommendations"),
        };
    }
}
