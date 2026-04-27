using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CareerForge.Application.Abstractions.Llm;
using Microsoft.Extensions.Options;

namespace CareerForge.Infrastructure.Llm.Gemini;

/// <summary>Chat-completion provider backed by the Gemini <c>generateContent</c> REST endpoint, with SSE streaming.</summary>
public sealed class GeminiLlmProvider(
    IHttpClientFactory httpFactory,
    IOptions<GeminiOptions> options)
    : ILlmProvider
{
    public const string ProviderName = "gemini";

    private readonly HttpClient _http = httpFactory.CreateClient(ProviderName);
    private readonly GeminiOptions _options = options.Value;

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
            throw new InvalidOperationException("Llm:Gemini:ApiKey is not configured.");

        var model = string.IsNullOrWhiteSpace(request.Model) ? _options.ChatModel : request.Model;
        var body = BuildRequest(request);
        var url = $"models/{model}:generateContent?key={_options.ApiKey}";
        using var response = await _http.PostAsJsonAsync(url, body, JsonOpts, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOpts, cancellationToken)
            ?? throw new InvalidOperationException("Empty Gemini response.");

        var content = payload.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
        return new LlmCompletionResponse(
            content,
            payload.UsageMetadata?.PromptTokenCount,
            payload.UsageMetadata?.CandidatesTokenCount);
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        LlmCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Llm:Gemini:ApiKey is not configured.");

        var model = string.IsNullOrWhiteSpace(request.Model) ? _options.ChatModel : request.Model;
        var body = BuildRequest(request);
        var url = $"models/{model}:streamGenerateContent?alt=sse&key={_options.ApiKey}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

            var json = line[6..];
            GeminiResponse? chunk;
            try { chunk = JsonSerializer.Deserialize<GeminiResponse>(json, JsonOpts); }
            catch (JsonException) { continue; }

            var text = chunk?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (!string.IsNullOrEmpty(text)) yield return text;
        }
    }

    private GeminiRequest BuildRequest(LlmCompletionRequest request) => new()
    {
        Contents = request.Messages
            .Select(m => new GeminiContent
            {
                Role = m.Role == LlmRole.Assistant ? "model" : "user",
                Parts = new[] { new GeminiPart { Text = m.Content } },
            })
            .ToArray(),
        SystemInstruction = string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? null
            : new GeminiContent { Parts = new[] { new GeminiPart { Text = request.SystemPrompt } } },
        GenerationConfig = new GeminiGenerationConfig
        {
            Temperature = request.Temperature,
            MaxOutputTokens = request.MaxOutputTokens,
            ResponseMimeType = request.JsonSchema is null ? null : "application/json",
            ResponseSchema = request.JsonSchema,
        },
    };

    private sealed class GeminiRequest
    {
        [JsonPropertyName("contents")] public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();
        [JsonPropertyName("systemInstruction")] public GeminiContent? SystemInstruction { get; set; }
        [JsonPropertyName("generationConfig")] public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonPropertyName("parts")] public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")] public double? Temperature { get; set; }
        [JsonPropertyName("maxOutputTokens")] public int? MaxOutputTokens { get; set; }
        [JsonPropertyName("responseMimeType")] public string? ResponseMimeType { get; set; }
        [JsonPropertyName("responseSchema")] public JsonNode? ResponseSchema { get; set; }
    }

    private sealed class GeminiResponse
    {
        [JsonPropertyName("candidates")] public GeminiCandidate[]? Candidates { get; set; }
        [JsonPropertyName("usageMetadata")] public GeminiUsage? UsageMetadata { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")] public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiUsage
    {
        [JsonPropertyName("promptTokenCount")] public int PromptTokenCount { get; set; }
        [JsonPropertyName("candidatesTokenCount")] public int CandidatesTokenCount { get; set; }
    }
}
