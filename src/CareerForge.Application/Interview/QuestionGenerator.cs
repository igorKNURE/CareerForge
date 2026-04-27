using System.Text.Json;
using System.Text.Json.Nodes;
using CareerForge.Application.Abstractions.Interview;
using CareerForge.Application.Abstractions.Llm;
using CareerForge.Application.Interview.Models;
using CareerForge.Application.JobDescriptions.Models;
using CareerForge.Application.Resumes.Models;
using CareerForge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CareerForge.Application.Interview;

/// <summary>
/// Drives the interview turn-by-turn: tries the curated question bank first, then falls back
/// through LLM tiers, with adaptive difficulty based on the user's recent scores.
/// </summary>
public sealed class QuestionGenerator(
    ILlmProviderFactory llmFactory,
    IQuestionBankSelector bank,
    ILogger<QuestionGenerator> logger)
    : IQuestionGenerator
{
    private static readonly (string Provider, string Model)[] Tiers =
    {
        ("gemini", "gemini-2.5-flash"),
        ("groq", "llama-3.3-70b-versatile"),
    };

    private static QuestionCategory[] CategoriesForTurn(int turn) => turn switch
    {
        1 => new[] { QuestionCategory.Behavioral, QuestionCategory.Technical, QuestionCategory.Resume },
        2 => new[] { QuestionCategory.Technical, QuestionCategory.Resume },
        3 or 4 => new[] { QuestionCategory.Technical, QuestionCategory.Resume },
        5 or 6 => new[] { QuestionCategory.Technical, QuestionCategory.SystemDesign },
        _ => new[] { QuestionCategory.SystemDesign, QuestionCategory.Coding, QuestionCategory.Technical },
    };

    private static (QuestionDifficulty Difficulty, QuestionCategory[] PreferredCategories, int Adjustment) TurnPlan(
        int turn, IReadOnlyList<PreviousTurn> previous)
    {
        var categories = CategoriesForTurn(turn);

        var lastEvaluated = previous
            .Where(p => p.OverallScore is not null && !p.EvaluationFailed)
            .LastOrDefault();

        var previousDifficulty = lastEvaluated is not null
            && Enum.TryParse<QuestionDifficulty>(lastEvaluated.Difficulty, ignoreCase: true, out var parsed)
            ? parsed
            : QuestionDifficulty.Easy;

        if (lastEvaluated is null)
            return (previousDifficulty, categories, 0);

        var adjustment = lastEvaluated.OverallScore!.Value switch
        {
            >= 80 => +1,
            <= 50 => -1,
            _ => 0,
        };
        var adjusted = (QuestionDifficulty)Math.Clamp(
            (int)previousDifficulty + adjustment,
            (int)QuestionDifficulty.Easy,
            (int)QuestionDifficulty.Hard);
        return (adjusted, categories, adjustment);
    }

    private const string SystemPrompt = """
        You are a supportive interview coach running a realistic practice session for a candidate.
        Your job: simulate the questions a real interviewer would ask for the target role.
        This is a TRAINING experience — be encouraging, but the questions themselves should be the same kind a real interviewer asks.

        For software engineering roles, the bulk of an interview is short technical-knowledge questions like:
        - "What's the difference between IQueryable and IEnumerable?"
        - "How does async/await actually work under the hood?"
        - "When would you choose a class over a struct?"
        - "Explain the difference between var, let, and const in JavaScript."
        - "What does the SOLID 'L' principle mean and give an example?"
        Plus some behavioral, resume, system-design, and coding questions mixed in.

        Difficulty calibration (the user message tells you which difficulty to use — match it exactly; do NOT pick your own):
        - Easy:   Foundational concept from the stack. Answerable in 1-2 minutes by a competent practitioner. e.g. "What's an interface and when do you use one?", "What does HTTP idempotency mean?"
        - Medium: Applied knowledge — when/why questions, common pitfalls, trade-offs. e.g. "Difference between Task and ValueTask, when does ValueTask win?", "How does the dependency-injection container resolve a Scoped service?"
        - Hard:   Internals, system design, debugging scenarios, architecture trade-offs. e.g. "Walk me through how you'd shard a write-heavy table", "Explain how the GC's generations interact with the LOH".

        Always:
        - The question MUST be specific to the candidate's actual skills/experience or to the JD's requirements. A generic warm-up (e.g. "tell me about yourself", "describe a recent technical decision") is NOT acceptable here — those live in a separate question bank. If you can't think of something specific to this stack/role, lean into the JD's must-have skills, the candidate's listed projects, or a named technology from either side.
        - Pick the topic from the candidate's stated skills OR the JD's must-have skills. Don't ask about Rust if neither side mentions it.
        - The 'difficulty' field in your output MUST be exactly one of: Easy, Medium, Hard — and MUST match the difficulty requested in the user message. No compound values like "Easy-Medium".
        - Pick the 'category' from the preferred categories listed in the user message when reasonable.
        - Keep technical-knowledge questions SHORT — one or two sentences. The candidate's answer is the substance; the question shouldn't lecture.
        - For technical-knowledge questions use category 'Technical' and format 'Freeform'. For "explain how X works" or "design X" use 'StepByStep'. For STAR-style use 'Behavioral'. For "walk me through your project" use 'Resume' / 'StructuredBullets'.
        - Never repeat a question already asked.
        - Vary categories across turns; don't cluster.
        - Ground in actual context: their real skills/experience or the real JD requirements.
        - Question text ≤ 50 words. Rationale: one sentence.
        """;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LenientEnumConverterFactory() },
    };

    public async Task<GeneratedQuestion> GenerateAsync(
        ResumeProfile resume,
        JobProfile vacancy,
        IReadOnlyList<PreviousTurn> previousTurns,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var lang = LanguageInstruction.Normalize(language);
        var turnNumber = previousTurns.Count + 1;
        var (turnDifficulty, preferredCategories, difficultyAdjustment) = TurnPlan(turnNumber, previousTurns);
        if (difficultyAdjustment != 0)
            logger.LogInformation("Turn {Turn} difficulty adjusted by {Delta} based on recent answer scores → {Difficulty}",
                turnNumber, difficultyAdjustment, turnDifficulty);
        var askedTexts = previousTurns.Select(p => p.Question).ToList();

        var jdSkills = (vacancy.MustHaveSkills ?? new List<string>())
            .Concat(vacancy.NiceToHaveSkills ?? new List<string>())
            .ToList();

        var banked = await bank.PickAsync(turnDifficulty, preferredCategories, jdSkills, askedTexts, lang, cancellationToken);
        if (banked is not null)
        {
            logger.LogInformation("Question gen tier 0 (bank) hit on turn {Turn}: matched {SkillCount} skill(s), category {Category}",
                turnNumber, banked.MatchedSkillCount, banked.Category);
            return new GeneratedQuestion(
                Text: banked.Text,
                Category: banked.Category,
                Difficulty: banked.Difficulty,
                ExpectedFormat: banked.ExpectedFormat,
                Rationale: banked.MatchedSkillCount > 0
                    ? $"From the question bank — overlaps with {banked.MatchedSkillCount} of the role's required skills."
                    : "From the question bank — appropriate warm-up for this turn.");
        }

        logger.LogInformation("Question gen bank miss on turn {Turn}; falling through to LLM tiers", turnNumber);
        var schema = BuildSchema();

        var previousBlock = previousTurns.Count == 0
            ? "(none — this is the very first question of the session)"
            : string.Join("\n", previousTurns.Select(p => $"- [{p.Category}] {p.Question}"));

        var recentScoreLine = previousTurns
            .Where(p => p.OverallScore is not null && !p.EvaluationFailed)
            .TakeLast(3)
            .Select(p => p.OverallScore!.Value.ToString())
            .ToList() is { Count: > 0 } scores
            ? $"Recent answer scores (most recent last): {string.Join(", ", scores)}"
            : "No graded answers yet.";

        var prompt = $"""
            ## Turn {turnNumber}

            ## Required difficulty for this question
            {turnDifficulty}

            ## Preferred categories for this question (pick one)
            {string.Join(", ", preferredCategories)}

            ## Recent performance signal
            {recentScoreLine}

            ## Candidate profile
            {JsonSerializer.Serialize(resume, JsonOpts)}

            ## Target role
            {JsonSerializer.Serialize(vacancy, JsonOpts)}

            ## Previous questions in this session
            {previousBlock}

            Generate the next interview question for Turn {turnNumber} at the required difficulty above. Set the 'difficulty' field in your JSON output to exactly "{turnDifficulty}".
            """;

        var systemPrompt = SystemPrompt + LanguageInstruction.Build(lang);

        for (var i = 0; i < Tiers.Length; i++)
        {
            var (provider, model) = Tiers[i];
            var attempt = await TryGenerateAsync(provider, model, systemPrompt, prompt, schema, cancellationToken);
            if (attempt is not null)
            {
                if (i > 0) logger.LogInformation("Question gen succeeded on tier {Tier} ({Provider}/{Model})", i + 1, provider, model);
                return attempt;
            }
        }

        logger.LogWarning("All question-gen tiers failed; returning static fallback");
        return Fallback(previousTurns.Count, lang);
    }

    private async Task<GeneratedQuestion?> TryGenerateAsync(
        string providerName, string model, string systemPrompt, string prompt, JsonNode schema, CancellationToken cancellationToken)
    {
        string? raw = null;
        try
        {
            var llm = llmFactory.Get(providerName);
            var response = await llm.CompleteAsync(new LlmCompletionRequest(
                Model: model,
                SystemPrompt: systemPrompt,
                Messages: new[] { new LlmMessage(LlmRole.User, prompt) },
                Temperature: 0.6,
                MaxOutputTokens: 4096,
                JsonSchema: schema), cancellationToken);
            raw = response.Content;

            var question = JsonSerializer.Deserialize<GeneratedQuestion>(raw, JsonOpts);
            if (question is null || string.IsNullOrWhiteSpace(question.Text)) return null;
            return question;
        }
        catch (JsonException jex)
        {
            var snippet = raw is null ? "(no body)" : raw.Length > 500 ? raw[..500] : raw;
            logger.LogWarning(jex, "Question gen JSON parse failed on {Provider}/{Model}. Snippet: {Snippet}",
                providerName, model, snippet);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Question gen on {Provider}/{Model} failed", providerName, model);
            return null;
        }
    }

    private static GeneratedQuestion Fallback(int turnIndex, string lang)
    {
        var isUk = lang == "uk";
        return turnIndex == 0
            ? new GeneratedQuestion(
                Text: isUk
                    ? "Розкажіть про нещодавній проєкт, який ви вели від початку до кінця. Яка була мета, які виникли труднощі та як ви з ними впоралися?"
                    : "Walk me through a recent project you led end-to-end. What was the goal, what challenges came up, and how did you navigate them?",
                Category: QuestionCategory.Behavioral,
                Difficulty: QuestionDifficulty.Medium,
                ExpectedFormat: AnswerFormat.STAR,
                Rationale: isUk
                    ? "Стандартне вступне питання (відповідь LLM не вдалося розібрати; спробуйте ще раз для персоналізованого питання)."
                    : "Default opener (LLM response could not be parsed; retry to get a tailored question).")
            : new GeneratedQuestion(
                Text: isUk
                    ? "Розкажіть про технічне рішення, яке ви нещодавно ухвалили. Які були альтернативи й чому ви обрали саме цей варіант?"
                    : "Tell me about a technical decision you made recently. What were the alternatives, and why did you pick the one you did?",
                Category: QuestionCategory.Technical,
                Difficulty: QuestionDifficulty.Medium,
                ExpectedFormat: AnswerFormat.StepByStep,
                Rationale: isUk
                    ? "Стандартне питання (відповідь LLM не вдалося розібрати; спробуйте ще раз для персоналізованого питання)."
                    : "Default question (LLM response could not be parsed; retry for a tailored one).");
    }

    private static JsonNode BuildSchema()
    {
        return new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["text"] = new JsonObject { ["type"] = "STRING", ["description"] = "The question, ≤ 80 words" },
                ["category"] = new JsonObject
                {
                    ["type"] = "STRING",
                    ["enum"] = new JsonArray("Behavioral", "Resume", "Technical", "SystemDesign", "Coding", "Other"),
                },
                ["difficulty"] = new JsonObject
                {
                    ["type"] = "STRING",
                    ["enum"] = new JsonArray("Easy", "Medium", "Hard"),
                },
                ["expectedFormat"] = new JsonObject
                {
                    ["type"] = "STRING",
                    ["enum"] = new JsonArray("STAR", "StructuredBullets", "StepByStep", "Freeform"),
                },
                ["rationale"] = new JsonObject { ["type"] = "STRING", ["description"] = "One sentence on why this question fits now" },
            },
            ["required"] = new JsonArray("text", "category", "difficulty", "expectedFormat", "rationale"),
        };
    }
}
