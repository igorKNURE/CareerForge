using CareerForge.Application.Abstractions.Llm;
using Microsoft.Extensions.Options;

namespace CareerForge.Infrastructure.Llm.Factories;

/// <summary>Resolves embedding providers from the DI registration set, keyed by their <see cref="IEmbeddingProvider.Name"/>.</summary>
public sealed class EmbeddingProviderFactory(
    IEnumerable<IEmbeddingProvider> providers,
    IOptions<LlmOptions> options)
    : IEmbeddingProviderFactory
{
    private readonly Dictionary<string, IEmbeddingProvider> _byName =
        providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    private readonly LlmOptions _options = options.Value;

    public IReadOnlyCollection<string> AvailableProviders => _byName.Keys;

    public IEmbeddingProvider GetDefault() => Get(_options.DefaultEmbeddingProvider);

    public IEmbeddingProvider Get(string name)
    {
        if (_byName.TryGetValue(name, out var provider))
            return provider;
        throw new InvalidOperationException(
            $"Embedding provider '{name}' is not registered. Available: {string.Join(", ", _byName.Keys)}");
    }
}
