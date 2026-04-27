namespace CareerForge.Application.Abstractions.Llm;

/// <summary>
/// Resolves <see cref="IEmbeddingProvider"/> instances by name, with a configured default.
/// </summary>
public interface IEmbeddingProviderFactory
{
    IEmbeddingProvider GetDefault();
    IEmbeddingProvider Get(string name);
    IReadOnlyCollection<string> AvailableProviders { get; }
}
