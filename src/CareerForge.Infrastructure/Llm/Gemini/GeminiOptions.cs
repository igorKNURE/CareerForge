namespace CareerForge.Infrastructure.Llm.Gemini;

/// <summary>Configuration for the Google Gemini chat + embedding providers.</summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Llm:Gemini";

    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta";
    public string ChatModel { get; init; } = "gemini-2.5-flash-lite";
    public string EmbeddingModel { get; init; } = "gemini-embedding-001";
    public int EmbeddingDimensions { get; init; } = 768;
}
