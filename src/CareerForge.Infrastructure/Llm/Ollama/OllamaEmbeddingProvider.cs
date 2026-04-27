using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareerForge.Application.Abstractions.Llm;
using Microsoft.Extensions.Options;

namespace CareerForge.Infrastructure.Llm.Ollama;

/// <summary>Embedding provider backed by a locally hosted Ollama server.</summary>
public sealed class OllamaEmbeddingProvider(
    IHttpClientFactory httpFactory,
    IOptions<OllamaOptions> options)
    : IEmbeddingProvider
{
    public const string ProviderName = "ollama";

    private readonly HttpClient _http = httpFactory.CreateClient(ProviderName);
    private readonly OllamaOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Name => ProviderName;
    public int Dimensions => _options.EmbeddingDimensions;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var body = new EmbedRequest { Model = _options.EmbeddingModel, Input = new[] { text } };
        using var response = await _http.PostAsJsonAsync("api/embed", body, JsonOpts, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<EmbedResponse>(JsonOpts, cancellationToken)
            ?? throw new InvalidOperationException("Empty Ollama embed response.");
        return payload.Embeddings?.FirstOrDefault() ?? Array.Empty<float>();
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var body = new EmbedRequest { Model = _options.EmbeddingModel, Input = texts.ToArray() };
        using var response = await _http.PostAsJsonAsync("api/embed", body, JsonOpts, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<EmbedResponse>(JsonOpts, cancellationToken)
            ?? throw new InvalidOperationException("Empty Ollama embed response.");
        return payload.Embeddings ?? Array.Empty<float[]>();
    }

    private sealed class EmbedRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("input")] public string[] Input { get; set; } = Array.Empty<string>();
    }

    private sealed class EmbedResponse
    {
        [JsonPropertyName("embeddings")] public float[][]? Embeddings { get; set; }
    }
}
