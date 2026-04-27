using System.Text.Json.Nodes;

namespace CareerForge.Application.Abstractions.Llm;

/// <summary>
/// Provider-agnostic chat-completion API; concrete implementations wrap Gemini, Groq, Ollama, etc.
/// </summary>
public interface ILlmProvider
{
    string Name { get; }

    Task<LlmCompletionResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamCompletionAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single LLM call. <see cref="JsonSchema"/>, when set, requests structured-output mode.
/// </summary>
public sealed record LlmCompletionRequest(
    string Model,
    string SystemPrompt,
    IReadOnlyList<LlmMessage> Messages,
    double? Temperature = null,
    int? MaxOutputTokens = null,
    JsonNode? JsonSchema = null);

/// <summary>Result of a non-streaming LLM call.</summary>
public sealed record LlmCompletionResponse(
    string Content,
    int? PromptTokens,
    int? CompletionTokens);

/// <summary>One message in an LLM conversation.</summary>
public sealed record LlmMessage(LlmRole Role, string Content);

/// <summary>Author of an <see cref="LlmMessage"/>.</summary>
public enum LlmRole
{
    User,
    Assistant,
}
