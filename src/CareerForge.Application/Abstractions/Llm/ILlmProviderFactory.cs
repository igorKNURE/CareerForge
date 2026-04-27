namespace CareerForge.Application.Abstractions.Llm;

/// <summary>
/// Resolves <see cref="ILlmProvider"/> instances by name, with a configured default.
/// </summary>
public interface ILlmProviderFactory
{
    ILlmProvider GetDefault();
    ILlmProvider Get(string name);
    IReadOnlyCollection<string> AvailableProviders { get; }
}
