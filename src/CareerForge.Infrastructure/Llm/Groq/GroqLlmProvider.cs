using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareerForge.Application.Abstractions.Llm;
using Microsoft.Extensions.Options;

namespace CareerForge.Infrastructure.Llm.Groq;

/// <summary>
/// Chat-completion provider for Groq's OpenAI-compatible API. JSON-schema requests are emulated by
/// appending the schema to the system prompt and asking for <c>response_format=json_object</c>;
/// streaming is not supported.
/// </summary>
public sealed class GroqLlmProvider(
    IHttpClientFactory httpFactory,
    IOptions<GroqOptions> options)
    : ILlmProvider
{
    public const string ProviderName = "groq";

    private readonly HttpClient _http = httpFactory.CreateClient(ProviderName);
    private readonly GroqOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Name => ProviderName;

    public async Task<LlmCompletionResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Llm:Groq:ApiKey is not configured.");

        var model = string.IsNullOrWhiteSpace(request.Model) ? _options.ChatModel : request.Model;

        var systemPrompt = request.SystemPrompt ?? string.Empty;
        if (request.JsonSchema is not null)
        {
            var schemaJson = request.JsonSchema.ToJsonString();
            systemPrompt = systemPrompt.TrimEnd() +
                "\n\nRespond with a single valid JSON object only — no prose, no markdown fences. " +
                "The JSON must conform to this schema (note: types may use uppercase Gemini-style names, " +
                "treat them as standard JSON Schema types):\n" + schemaJson;
        }

        var messages = new List<GroqMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            messages.Add(new() { Role = "system", Content = systemPrompt });
        foreach (var m in request.Messages)
            messages.Add(new() { Role = m.Role == LlmRole.Assistant ? "assistant" : "user", Content = m.Content });

        var body = new GroqRequest
        {
            Model = model,
            Messages = messages.ToArray(),
            Temperature = request.Temperature,
            MaxTokens = request.MaxOutputTokens,
            ResponseFormat = request.JsonSchema is null ? null : new GroqResponseFormat { Type = "json_object" },
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _http.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Groq returned {(int)response.StatusCode}: {Truncate(errorBody, 500)}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<GroqResponse>(JsonOpts, cancellationToken)
            ?? throw new InvalidOperationException("Empty Groq response.");

        var content = payload.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        return new LlmCompletionResponse(content, payload.Usage?.PromptTokens, payload.Usage?.CompletionTokens);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    public IAsyncEnumerable<string> StreamCompletionAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Groq streaming is not implemented; use Gemini or Ollama for streaming.");
    }

    private sealed class GroqRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public GroqMessage[] Messages { get; set; } = Array.Empty<GroqMessage>();
        [JsonPropertyName("temperature")] public double? Temperature { get; set; }
        [JsonPropertyName("max_tokens")] public int? MaxTokens { get; set; }
        [JsonPropertyName("response_format")] public GroqResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class GroqMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private sealed class GroqResponseFormat
    {
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    }

    private sealed class GroqResponse
    {
        [JsonPropertyName("choices")] public GroqChoice[]? Choices { get; set; }
        [JsonPropertyName("usage")] public GroqUsage? Usage { get; set; }
    }

    private sealed class GroqChoice
    {
        [JsonPropertyName("message")] public GroqMessage? Message { get; set; }
    }

    private sealed class GroqUsage
    {
        [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
    }
}
