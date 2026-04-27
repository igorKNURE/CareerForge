using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CareerForge.Application.Abstractions.Llm;
using Microsoft.Extensions.Options;

namespace CareerForge.Infrastructure.Llm.Ollama;

/// <summary>Chat-completion provider for a locally hosted Ollama server, with line-delimited JSON streaming.</summary>
public sealed class OllamaLlmProvider(
    IHttpClientFactory httpFactory,
    IOptions<OllamaOptions> options)
    : ILlmProvider
{
    public const string ProviderName = "ollama";

    private readonly HttpClient _http = httpFactory.CreateClient(ProviderName);
    private readonly OllamaOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Name => ProviderName;

    public async Task<LlmCompletionResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? _options.ChatModel : request.Model;
        var messages = BuildMessages(request);

        var body = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Stream = false,
            Format = request.JsonSchema,
            Options = new OllamaInferenceOptions
            {
                Temperature = request.Temperature,
                NumPredict = request.MaxOutputTokens,
            },
        };

        using var response = await _http.PostAsJsonAsync("api/chat", body, JsonOpts, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOpts, cancellationToken)
            ?? throw new InvalidOperationException("Empty Ollama response.");

        return new LlmCompletionResponse(
            payload.Message?.Content ?? string.Empty,
            payload.PromptEvalCount,
            payload.EvalCount);
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        LlmCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? _options.ChatModel : request.Model;
        var messages = BuildMessages(request);
        var body = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Stream = true,
            Format = request.JsonSchema,
            Options = new OllamaInferenceOptions
            {
                Temperature = request.Temperature,
                NumPredict = request.MaxOutputTokens,
            },
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat")
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

            OllamaChatResponse? chunk;
            try { chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOpts); }
            catch (JsonException) { continue; }

            var text = chunk?.Message?.Content;
            if (!string.IsNullOrEmpty(text)) yield return text;
        }
    }

    private static OllamaMessage[] BuildMessages(LlmCompletionRequest request)
    {
        var messages = new List<OllamaMessage>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new OllamaMessage { Role = "system", Content = request.SystemPrompt });
        messages.AddRange(request.Messages.Select(m => new OllamaMessage
        {
            Role = m.Role == LlmRole.Assistant ? "assistant" : "user",
            Content = m.Content,
        }));
        return messages.ToArray();
    }

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public OllamaMessage[] Messages { get; set; } = Array.Empty<OllamaMessage>();
        [JsonPropertyName("stream")] public bool Stream { get; set; }
        [JsonPropertyName("format")] public JsonNode? Format { get; set; }
        [JsonPropertyName("options")] public OllamaInferenceOptions? Options { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaInferenceOptions
    {
        [JsonPropertyName("temperature")] public double? Temperature { get; set; }
        [JsonPropertyName("num_predict")] public int? NumPredict { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
        [JsonPropertyName("prompt_eval_count")] public int? PromptEvalCount { get; set; }
        [JsonPropertyName("eval_count")] public int? EvalCount { get; set; }
    }
}
