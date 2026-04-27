namespace CareerForge.Infrastructure.Llm.Groq;

/// <summary>Configuration for the Groq OpenAI-compatible chat completions provider.</summary>
public sealed class GroqOptions
{
    public const string SectionName = "Llm:Groq";

    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.groq.com/openai/v1/";
    public string ChatModel { get; init; } = "llama-3.3-70b-versatile";
}
