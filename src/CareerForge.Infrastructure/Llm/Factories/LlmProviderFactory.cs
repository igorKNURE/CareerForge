using CareerForge.Application.Abstractions.Llm;
using Microsoft.Extensions.Options;

namespace CareerForge.Infrastructure.Llm.Factories;

/// <summary>Resolves LLM providers from the DI registration set, keyed by their <see cref="ILlmProvider.Name"/>.</summary>
public sealed class LlmProviderFactory(
    IEnumerable<ILlmProvider> providers,
    IOptions<LlmOptions> options)
    : ILlmProviderFactory
{
    private readonly Dictionary<string, ILlmProvider> _byName =
        providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    private readonly LlmOptions _options = options.Value;

    public IReadOnlyCollection<string> AvailableProviders => _byName.Keys;

    public ILlmProvider GetDefault() => Get(_options.DefaultLlmProvider);

    public ILlmProvider Get(string name)
    {
        if (_byName.TryGetValue(name, out var provider))
            return provider;
        throw new InvalidOperationException(
            $"LLM provider '{name}' is not registered. Available: {string.Join(", ", _byName.Keys)}");
    }
}
