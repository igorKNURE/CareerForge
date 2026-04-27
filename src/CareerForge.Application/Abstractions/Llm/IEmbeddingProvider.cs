namespace CareerForge.Application.Abstractions.Llm;

/// <summary>
/// Provider-agnostic text-embedding API; <see cref="Dimensions"/> must match the pgvector column width.
/// </summary>
public interface IEmbeddingProvider
{
    string Name { get; }
    int Dimensions { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
