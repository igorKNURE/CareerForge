namespace CareerForge.Infrastructure.Llm.Ollama;

/// <summary>Configuration for a local Ollama server used as an LLM and embedding fallback.</summary>
public sealed class OllamaOptions
{
    public const string SectionName = "Llm:Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string ChatModel { get; init; } = "qwen2.5:7b";
    public string EmbeddingModel { get; init; } = "nomic-embed-text";
    public int EmbeddingDimensions { get; init; } = 768;
}
