using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using CareerForge.Application.Abstractions.Llm;
using CareerForge.Application.Abstractions.Matching;
using CareerForge.Application.JobDescriptions.Models;
using CareerForge.Application.Matching.Models;
using CareerForge.Application.Resumes.Models;
using CareerForge.Domain.Enums;
using CareerForge.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CareerForge.Application.Matching;

/// <summary>
/// Scores a resume against a vacancy by combining deterministic skill / experience / semantic
/// signals with an LLM-generated findings layer that has multi-tier provider fallbacks.
/// </summary>
public sealed class MatchScorer(
    ILlmProviderFactory llmFactory,
    IEmbeddingProviderFactory embeddingFactory,
    ILogger<MatchScorer> logger)
    : IMatchScorer
{
    private const decimal SkillCoverageWeight = 0.45m;
    private const decimal SemanticWeight = 0.20m;
    private const decimal ExperienceWeight = 0.35m;

    private const string PrimaryProvider = "gemini";
    private const string PrimaryFindingsModel = "gemini-2.5-flash";
    private const string SecondaryProvider = "groq";
    private const string SecondaryFindingsModel = "llama-3.3-70b-versatile";
    private const string ProseFallbackModel = "gemini-2.5-flash-lite";

    private const string FindingsSystemPrompt = """
        You are a career coach speaking DIRECTLY to a candidate about how their resume matches a job description.

        ===== HARD RULES (violating any is a failure) =====

        RULE 1 — Voice: SECOND PERSON only ("you", "your"). In Ukrainian use formal "ви/вам/ваш". NEVER use the candidate's name, "the candidate", "they", "he/she", "his/her".

        RULE 2 — Anti-hallucination: The user message includes a "## SKILLS PRESENT IN THE RESUME" list and full experience highlights. You MUST NOT claim a skill from that list is missing, absent, or "not mentioned". You MUST NOT claim a topic is missing from the resume if any experience highlight or the summary contains the word or an obvious synonym.
          - If REACT, BLAZOR, or JAVASCRIPT are in the present list → forbidden to say "no front-end experience".
          - If a highlight contains "AI" / "ML" / "Python AI API" → forbidden to say "no AI experience".
          - If a highlight contains "Agile team" / "code reviews" / a "TEAM COLLABORATION" skill → forbidden to say "no teamwork".
          - If a highlight contains a percentage or numeric improvement (e.g. "40%", "25%") → forbidden to say "no metrics".
          Before drafting any finding, mentally check: "is this actually absent, or did I just skim?"

        RULE 3 — One finding per root cause: Don't split facets of the same gap into separate findings. The years gap AND the seniority-framing mismatch are ONE finding, not two.

        RULE 4 — At most 5 findings, ordered by severity. Fewer is better than padding.

        RULE 5 — Cite the resume: every finding's description must reference something concrete from the resume — a quoted phrase you read, a named project/role, or a specific bullet you're calling out. No vague platitudes.

        ===== BAD vs GOOD findings =====

        BAD (vague, generic, could apply to any resume — DO NOT WRITE LIKE THIS):
          title: "Insufficient experience emphasis"
          description: "Your work experience is not emphasized enough in the resume"
          recommendation: "Highlight your achievements and experience in relevant projects"
          → Wrong because: doesn't quote anything, doesn't name a specific bullet, gives advice that fits any resume.

        GOOD (specific, grounded, actionable):
          title: "AI-integration impact buried in 3rd bullet"
          description: "You integrated a Python AI API at SmartAxis with a 25% latency win, but it's the third bullet of your most recent role. The JD opens by describing the role as 'rearchitecting their AI-powered SaaS platform' — your strongest matching signal is hiding."
          recommendation: "Lead the SmartAxis bullets with the AI-integration line, and add an AI-integration phrase to your summary."
          resumeExcerpt: "Integrated the backend with a Python-based AI API, reducing document analysis time by ~25%."

        Every finding must look more like the GOOD example than the BAD one.

        ===== Output format =====

        For each finding:
          - severity: Critical (must-have missing/weak), Warning (nice-to-have gap or weak signal), Info (polish).
          - category: missing-skill, experience-gap, weak-signal, keyword-coverage, framing.
          - title: under 80 chars.
          - description: under 200 chars, addressed to "you", grounded in resume content.
          - recommendation: under 200 chars, direct advice ("add X", "lead with Y", "quantify Z"). NOT a paraphrase of the title.
          - resumeExcerpt: brief quote from the resume (under 120 chars) if directly relevant, else empty.

        Then improvementSummary: 2–3 sentences (under 400 chars), the top moves you'd make. Direct voice.

        Be honest and specific. If the resume already covers something well, don't raise it as a finding at all.
        """;

    private const string ProseFallbackSystemPrompt = """
        You are a career coach speaking DIRECTLY to the candidate. Their resume and the target job profile are below, along with pre-computed match scores.

        Voice: address them in the SECOND PERSON ("you", "your"). In Ukrainian, use the formal "ви/вам/ваш". NEVER refer to the candidate by name or in the third person.

        Write 2-3 short paragraphs of practical, prioritised feedback on how to improve the resume to better fit this role.
        Be concrete and grounded in what's actually in the resume; do not invent experience.
        Plain prose only — no headings, no bullets, no markdown. Under 250 words.
        """;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<MatchScoringResult> ScoreAsync(
        ResumeProfile resume,
        JobProfile vacancy,
        float[]? cachedResumeEmbedding = null,
        float[]? cachedVacancyEmbedding = null,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var lang = LanguageInstruction.Normalize(language);
        var (matchedMust, missingMust) = CategoriseSkills(vacancy.MustHaveSkills, resume.Skills);
        var (matchedNice, _) = CategoriseSkills(vacancy.NiceToHaveSkills, resume.Skills);

        var skillCoverage = ComputeSkillCoverage(matchedMust.Count, missingMust.Count, matchedNice.Count, vacancy.NiceToHaveSkills.Count);
        var semantic = await ComputeSemanticSimilarityAsync(
            resume.Summary, vacancy.Summary,
            cachedResumeEmbedding, cachedVacancyEmbedding,
            cancellationToken);
        var experienceFit = ComputeExperienceFit(resume.YearsOfExperience, vacancy.YearsRequired);

        var overall = decimal.Round(
            skillCoverage * SkillCoverageWeight +
            semantic * SemanticWeight +
            experienceFit * ExperienceWeight,
            2);

        var (findings, improvementSummary) = await GenerateFindingsAsync(
            resume, vacancy, matchedMust, missingMust, matchedNice,
            skillCoverage, semantic, experienceFit, lang, cancellationToken);

        return new MatchScoringResult(
            OverallScore: overall,
            SkillCoverageScore: skillCoverage,
            SemanticSimilarityScore: semantic,
            ExperienceFitScore: experienceFit,
            MatchedMustHaveSkills: matchedMust,
            MissingMustHaveSkills: missingMust,
            MatchedNiceToHaveSkills: matchedNice,
            Findings: findings,
            ImprovementSummary: improvementSummary);
    }

    private static (IReadOnlyList<string> matched, IReadOnlyList<string> missing) CategoriseSkills(
        IReadOnlyList<string> required,
        IReadOnlyList<string> candidateSkills)
    {
        var candidateTokens = candidateSkills.Select(SignificantTokens).ToList();

        var matched = new List<string>();
        var missing = new List<string>();
        foreach (var req in required)
        {
            var reqTokens = SignificantTokens(req);
            if (reqTokens.Count == 0)
            {
                missing.Add(req);
                continue;
            }
            // Bidirectional subset: a JD skill is satisfied if ANY single resume skill's
            // significant tokens are either a subset OR a superset of the JD skill's tokens.
            // - candidate ⊆ JD handles JDs that phrase requirements broadly
            //   (resume "ASP.NET" {asp,net} ⊆ JD "ASP.NET Core for REST API" {asp,net,core,rest}).
            // - JD ⊆ candidate handles JDs that name a general thing the resume specialises
            //   (JD "API" {api} ⊆ resume "REST APIs" {rest,api}).
            var hasMatch = candidateTokens.Any(c =>
                c.Count > 0 && (c.IsSubsetOf(reqTokens) || reqTokens.IsSubsetOf(c)));

            if (hasMatch) matched.Add(req);
            else missing.Add(req);
        }
        return (matched, missing);
    }

    private static decimal ComputeSkillCoverage(int matchedMust, int missingMust, int matchedNice, int totalNice)
    {
        var totalMust = matchedMust + missingMust;
        if (totalMust == 0 && totalNice == 0) return 100m;

        decimal mustScore = totalMust == 0 ? 100m : (decimal)matchedMust / totalMust * 100m;
        decimal niceScore = totalNice == 0 ? 100m : (decimal)matchedNice / totalNice * 100m;

        return decimal.Round(mustScore * 0.8m + niceScore * 0.2m, 2);
    }

    private async Task<decimal> ComputeSemanticSimilarityAsync(
        string resumeSummary,
        string jdSummary,
        float[]? cachedResumeEmbedding,
        float[]? cachedVacancyEmbedding,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(resumeSummary) || string.IsNullOrWhiteSpace(jdSummary))
            return 0m;

        var resumeVec = cachedResumeEmbedding;
        var vacancyVec = cachedVacancyEmbedding;

        if (resumeVec is null || vacancyVec is null)
        {
            var embedder = embeddingFactory.GetDefault();
            var toEmbed = new List<string>();
            if (resumeVec is null) toEmbed.Add(resumeSummary);
            if (vacancyVec is null) toEmbed.Add(jdSummary);
            var fresh = await embedder.EmbedBatchAsync(toEmbed, ct);
            var idx = 0;
            if (resumeVec is null) resumeVec = fresh.ElementAtOrDefault(idx++);
            if (vacancyVec is null) vacancyVec = fresh.ElementAtOrDefault(idx);
        }

        if (resumeVec is null || vacancyVec is null) return 0m;
        var cosine = Cosine(resumeVec, vacancyVec);
        var clamped = Math.Max(0d, Math.Min(1d, cosine));
        return decimal.Round((decimal)clamped * 100m, 2);
    }

    private static decimal ComputeExperienceFit(double? candidateYears, double? requiredYears)
    {
        if (requiredYears is null || requiredYears <= 0) return 100m;
        if (candidateYears is null) return 50m;
        var ratio = Math.Min(1d, candidateYears.Value / requiredYears.Value);
        return decimal.Round((decimal)ratio * 100m, 2);
    }

    private async Task<(IReadOnlyList<MatchFinding>, string)> GenerateFindingsAsync(
        ResumeProfile resume,
        JobProfile vacancy,
        IReadOnlyList<string> matchedMust,
        IReadOnlyList<string> missingMust,
        IReadOnlyList<string> matchedNice,
        decimal skillCoverage,
        decimal semantic,
        decimal experienceFit,
        string lang,
        CancellationToken ct)
    {
        var schema = BuildSchema();
        var presentSkills = (resume.Skills ?? new List<string>())
            .Concat(matchedMust)
            .Concat(matchedNice)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resumeHighlightsBlock = BuildHighlightsBlock(resume);

        var prompt = $"""
            ## SKILLS PRESENT IN THE RESUME (do NOT claim these are missing)
            {(presentSkills.Count == 0 ? "(none listed)" : string.Join(", ", presentSkills))}

            ## Skills genuinely missing from the resume vs the JD
            Must-have NOT in resume: {(missingMust.Count == 0 ? "(none — all must-haves are covered)" : string.Join(", ", missingMust))}

            ## Deterministic component scores
            Skill coverage: {skillCoverage} / 100
            Semantic similarity: {semantic} / 100
            Experience fit: {experienceFit} / 100

            ## RESUME HIGHLIGHTS (you MUST quote or directly reference at least one of these in each finding's description)
            {resumeHighlightsBlock}

            ## Resume summary
            {resume.Summary}

            ## Job profile (full)
            {JsonSerializer.Serialize(vacancy, JsonOpts)}
            """;

        var findingsSystem = FindingsSystemPrompt + LanguageInstruction.Build(lang);
        var proseSystem = ProseFallbackSystemPrompt + LanguageInstruction.Build(lang);

        var resumeText = BuildResumeText(resume);

        var tier1 = await TryStructuredFindingsAsync(PrimaryProvider, PrimaryFindingsModel, findingsSystem, prompt, schema, resumeText, ct);
        if (tier1 is not null) return tier1.Value;

        logger.LogInformation("Findings tier 1 ({Provider}/{Model}) failed; trying tier 2 ({P2}/{M2})",
            PrimaryProvider, PrimaryFindingsModel, SecondaryProvider, SecondaryFindingsModel);

        var tier2 = await TryStructuredFindingsAsync(SecondaryProvider, SecondaryFindingsModel, findingsSystem, prompt, schema, resumeText, ct);
        if (tier2 is not null)
        {
            logger.LogInformation("Findings succeeded on tier 2 (Groq)");
            return tier2.Value;
        }

        logger.LogInformation("Findings tier 2 (Groq) failed; falling back to prose tier 3");
        var llm = llmFactory.Get(PrimaryProvider);
        return await TryProseFallbackAsync(llm, proseSystem, prompt, lang, ct);
    }

    private async Task<(IReadOnlyList<MatchFinding>, string)?> TryStructuredFindingsAsync(
        string providerName, string model, string systemPrompt, string prompt, JsonNode schema, string resumeText, CancellationToken ct)
    {
        try
        {
            var llm = llmFactory.Get(providerName);
            var response = await llm.CompleteAsync(new LlmCompletionRequest(
                Model: model,
                SystemPrompt: systemPrompt,
                Messages: new[] { new LlmMessage(LlmRole.User, prompt) },
                Temperature: 0.1,
                MaxOutputTokens: 8192,
                JsonSchema: schema), ct);

            FindingsPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<FindingsPayload>(response.Content, JsonOpts);
            }
            catch (JsonException jex)
            {
                logger.LogWarning(jex,
                    "Findings JSON parse failed on {Provider}/{Model} (len={Length}). Snippet: {Snippet}",
                    providerName, model, response.Content.Length,
                    response.Content.Length > 400 ? response.Content[..400] : response.Content);
                return null;
            }

            if (payload is null || payload.Findings.Count == 0 && string.IsNullOrWhiteSpace(payload.ImprovementSummary))
                return null;

            var findings = payload.Findings
                .Select(f => new MatchFinding(
                    ParseSeverity(f.Severity),
                    string.IsNullOrWhiteSpace(f.Category) ? "general" : f.Category,
                    f.Title,
                    f.Description,
                    string.IsNullOrWhiteSpace(f.Recommendation) ? null : f.Recommendation,
                    string.IsNullOrWhiteSpace(f.ResumeExcerpt) ? null : f.ResumeExcerpt))
                .Where(f => !IsHallucination(f, resumeText, providerName, model))
                .ToList();

            return (findings, payload.ImprovementSummary ?? string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Findings call to {Provider}/{Model} failed", providerName, model);
            return null;
        }
    }

    private async Task<(IReadOnlyList<MatchFinding>, string)> TryProseFallbackAsync(
        ILlmProvider llm, string systemPrompt, string prompt, string lang, CancellationToken ct)
    {
        try
        {
            var response = await llm.CompleteAsync(new LlmCompletionRequest(
                Model: ProseFallbackModel,
                SystemPrompt: systemPrompt,
                Messages: new[] { new LlmMessage(LlmRole.User, prompt) },
                Temperature: 0.3,
                MaxOutputTokens: 1024,
                JsonSchema: null), ct);
            var prose = response.Content.Trim();
            if (string.IsNullOrWhiteSpace(prose))
                return (Array.Empty<MatchFinding>(),
                    lang == "uk"
                        ? "Висновки ШІ зараз недоступні. Детерміновані оцінки вище залишаються коректними."
                        : "AI findings unavailable right now. Deterministic scores above are still valid.");
            logger.LogInformation("Findings prose fallback succeeded on {Model}", ProseFallbackModel);
            return (Array.Empty<MatchFinding>(), prose);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Prose fallback also failed");
            return (Array.Empty<MatchFinding>(),
                lang == "uk"
                    ? "Висновки ШІ зараз недоступні (ліміт запитів). Детерміновані оцінки вище залишаються коректними — спробуйте за хвилину."
                    : "AI findings unavailable right now (rate limit). Deterministic scores above are still valid — try again in a minute.");
        }
    }

    private static string BuildHighlightsBlock(ResumeProfile resume)
    {
        if (resume.Experience is null || resume.Experience.Count == 0)
            return "(no experience entries)";

        var sb = new System.Text.StringBuilder();
        var counter = 1;
        foreach (var exp in resume.Experience)
        {
            sb.Append('[').Append(exp.Role ?? "?")
              .Append(" @ ").Append(exp.Company ?? "?").Append(']').AppendLine();
            if (exp.Highlights is not null)
            {
                foreach (var h in exp.Highlights)
                {
                    if (string.IsNullOrWhiteSpace(h)) continue;
                    sb.Append(counter++).Append(". ").AppendLine(h.Trim());
                }
            }
        }
        return sb.Length == 0 ? "(no highlights)" : sb.ToString().TrimEnd();
    }

    private static string BuildResumeText(ResumeProfile resume)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(resume.FullName);
        sb.AppendLine(resume.Headline);
        sb.AppendLine(resume.Summary);
        if (resume.Skills is not null)
            foreach (var s in resume.Skills) sb.AppendLine(s);
        if (resume.Experience is not null)
            foreach (var e in resume.Experience)
            {
                sb.AppendLine(e.Company);
                sb.AppendLine(e.Role);
                if (e.Highlights is not null)
                    foreach (var h in e.Highlights) sb.AppendLine(h);
            }
        if (resume.Education is not null)
            foreach (var e in resume.Education)
            {
                sb.AppendLine(e.Institution);
                sb.AppendLine(e.Degree);
            }
        return sb.ToString().ToLowerInvariant();
    }

    private static readonly System.Text.RegularExpressions.Regex QuotedTermPattern =
        new(@"['""\u2018\u2019\u201C\u201D]([^'""\u2018\u2019\u201C\u201D]{1,40})['""\u2018\u2019\u201C\u201D]",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private bool IsHallucination(MatchFinding finding, string resumeTextLower, string providerName, string model)
    {
        // Only police categories that make falsifiable claims about absence.
        var category = finding.Category?.ToLowerInvariant() ?? string.Empty;
        if (!category.Contains("missing") && !category.Contains("keyword")) return false;

        var blob = (finding.Title ?? string.Empty) + " " + (finding.Description ?? string.Empty) + " " + (finding.Recommendation ?? string.Empty);

        // Extract terms the LLM placed in quotes (a common hallucination pattern: "doesn't contain 'SaaS'").
        foreach (System.Text.RegularExpressions.Match m in QuotedTermPattern.Matches(blob))
        {
            var term = m.Groups[1].Value.Trim();
            if (term.Length < 2) continue;
            var termLower = term.ToLowerInvariant();
            if (resumeTextLower.Contains(termLower))
            {
                logger.LogInformation(
                    "Dropping hallucinated finding from {Provider}/{Model}: claimed '{Term}' is missing but it's in the resume. Title: {Title}",
                    providerName, model, term, finding.Title);
                return true;
            }
        }
        return false;
    }

    private static FindingSeverity ParseSeverity(string raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "critical" => FindingSeverity.Critical,
        "warning" => FindingSeverity.Warning,
        _ => FindingSeverity.Info,
    };

    private static double Cosine(float[] a, float[] b)
    {
        var len = Math.Min(a.Length, b.Length);
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < len; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        var denom = Math.Sqrt(magA) * Math.Sqrt(magB);
        return denom == 0 ? 0 : dot / denom;
    }

    // Single-token aliases (after splitting & lowercasing).
    private static readonly Dictionary<string, string> TokenAliases = new(StringComparer.Ordinal)
    {
        ["dotnet"] = "net",
        ["csharp"] = "c#",
        ["js"] = "javascript",
        ["ts"] = "typescript",
        ["k8s"] = "kubernetes",
        ["gcp"] = "googlecloud",
        ["nodejs"] = "node",
        ["postgres"] = "postgresql",
        ["psql"] = "postgresql",
        ["aspnet"] = "asp",
        ["restful"] = "rest",
        ["apis"] = "api",
    };

    // Words so generic they don't indicate a specific skill on their own.
    // Kept tight on purpose: leaving real tech terms (core, api, framework, cloud, server)
    // in the token set lets bidirectional subset matching work.
    private static readonly HashSet<string> StopTokens = new(StringComparer.Ordinal)
    {
        // English connectors
        "or", "and", "for", "the", "a", "an", "of", "with", "to", "in", "on", "by", "as",
        "any", "all", "other", "etc",
        // Generic JD filler
        "experience", "knowledge", "proficiency", "expertise",
        "modern", "later", "newer", "ecosystem", "environment", "environments",
        "principles", "principle", "practice", "practices",
        "development", "developer", "engineering", "engineer", "programming",
        "based", "good", "strong", "deep",
        // Bare numbers
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
    };

    private static HashSet<string> SignificantTokens(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill)) return new HashSet<string>();
        var lower = skill.Trim().ToLowerInvariant();
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in System.Text.RegularExpressions.Regex.Split(lower, @"[^a-z0-9#+]+"))
        {
            if (string.IsNullOrEmpty(raw)) continue;
            var token = TokenAliases.TryGetValue(raw, out var alias) ? alias : raw;
            if (StopTokens.Contains(token)) continue;
            result.Add(token);
        }
        return result;
    }

    private static JsonNode BuildSchema()
    {
        return new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["findings"] = new JsonObject
                {
                    ["type"] = "ARRAY",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "OBJECT",
                        ["properties"] = new JsonObject
                        {
                            ["severity"] = new JsonObject { ["type"] = "STRING", ["description"] = "Critical, Warning, or Info" },
                            ["category"] = new JsonObject { ["type"] = "STRING" },
                            ["title"] = new JsonObject { ["type"] = "STRING" },
                            ["description"] = new JsonObject { ["type"] = "STRING" },
                            ["recommendation"] = new JsonObject { ["type"] = "STRING" },
                            ["resumeExcerpt"] = new JsonObject { ["type"] = "STRING" },
                        },
                    },
                },
                ["improvementSummary"] = new JsonObject { ["type"] = "STRING" },
            },
            ["required"] = new JsonArray("findings", "improvementSummary"),
        };
    }

    private sealed record FindingsPayload(IReadOnlyList<RawFinding> Findings, string ImprovementSummary);
    private sealed record RawFinding(string Severity, string Category, string Title, string Description, string Recommendation, string ResumeExcerpt);
}
