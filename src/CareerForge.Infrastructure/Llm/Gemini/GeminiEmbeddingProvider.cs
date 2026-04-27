using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareerForge.Application.Abstractions.Llm;
using Microsoft.Extensions.Options;

namespace CareerForge.Infrastructure.Llm.Gemini;

/// <summary>Embedding provider backed by the Gemini <c>embedContent</c> / <c>batchEmbedContents</c> endpoints.</summary>
public sealed class GeminiEmbeddingProvider(
    IHttpClientFactory httpFactory,
    IOptions<GeminiOptions> options)
    : IEmbeddingProvider
{
    public const string ProviderName = "gemini";

    private readonly HttpClient _http = httpFactory.CreateClient(ProviderName);
    private readonly GeminiOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Name => ProviderName;
    public int Dimensions => _options.EmbeddingDimensions;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        Ensure();
        var body = new SingleRequest
        {
            Content = new Content { Parts = new[] { new Part { Text = text } } },
            OutputDimensionality = _options.EmbeddingDimensions,
        };
        var url = $"models/{_options.EmbeddingModel}:embedContent?key={_options.ApiKey}";
        using var response = await _http.PostAsJsonAsync(url, body, JsonOpts, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<SingleResponse>(JsonOpts, cancellationToken)
            ?? throw new InvalidOperationException("Empty Gemini embed response.");
        return payload.Embedding?.Values ?? Array.Empty<float>();
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        Ensure();
        var body = new BatchRequest
        {
            Requests = texts.Select(t => new BatchItem
            {
                Model = $"models/{_options.EmbeddingModel}",
                Content = new Content { Parts = new[] { new Part { Text = t } } },
                OutputDimensionality = _options.EmbeddingDimensions,
            }).ToArray(),
        };
        var url = $"models/{_options.EmbeddingModel}:batchEmbedContents?key={_options.ApiKey}";
        using var response = await _http.PostAsJsonAsync(url, body, JsonOpts, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BatchResponse>(JsonOpts, cancellationToken)
            ?? throw new InvalidOperationException("Empty Gemini batch embed response.");
        return payload.Embeddings?.Select(e => e.Values ?? Array.Empty<float>()).ToArray() ?? Array.Empty<float[]>();
    }

    private void Ensure()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Llm:Gemini:ApiKey is not configured.");
    }

    private sealed class SingleRequest
    {
        [JsonPropertyName("content")] public Content Content { get; set; } = new();
        [JsonPropertyName("outputDimensionality")] public int? OutputDimensionality { get; set; }
    }

    private sealed class BatchRequest
    {
        [JsonPropertyName("requests")] public BatchItem[] Requests { get; set; } = Array.Empty<BatchItem>();
    }

    private sealed class BatchItem
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("content")] public Content Content { get; set; } = new();
        [JsonPropertyName("outputDimensionality")] public int? OutputDimensionality { get; set; }
    }

    private sealed class Content
    {
        [JsonPropertyName("parts")] public Part[] Parts { get; set; } = Array.Empty<Part>();
    }

    private sealed class Part
    {
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }

    private sealed class SingleResponse
    {
        [JsonPropertyName("embedding")] public Embedding? Embedding { get; set; }
    }

    private sealed class BatchResponse
    {
        [JsonPropertyName("embeddings")] public Embedding[]? Embeddings { get; set; }
    }

    private sealed class Embedding
    {
        [JsonPropertyName("values")] public float[]? Values { get; set; }
    }
}
