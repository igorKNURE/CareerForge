namespace CareerForge.Infrastructure.Llm;

/// <summary>Top-level LLM configuration: which registered provider names are the defaults.</summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string DefaultLlmProvider { get; init; } = "gemini";
    public string DefaultEmbeddingProvider { get; init; } = "gemini";
}
