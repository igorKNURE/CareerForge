using CareerForge.Application.Abstractions.Documents;

namespace CareerForge.Infrastructure.Documents;

/// <summary>Picks the first registered <see cref="IDocumentParser"/> that claims it can handle a given upload.</summary>
public sealed class DocumentParserFactory(IEnumerable<IDocumentParser> parsers) : IDocumentParserFactory
{
    private readonly IReadOnlyList<IDocumentParser> _parsers = parsers.ToList();

    public IDocumentParser Resolve(string contentType, string fileName)
    {
        if (TryResolve(contentType, fileName, out var parser) && parser is not null)
            return parser;
        throw new NotSupportedException(
            $"No parser registered for contentType='{contentType}', fileName='{fileName}'.");
    }

    public bool TryResolve(string contentType, string fileName, out IDocumentParser? parser)
    {
        parser = _parsers.FirstOrDefault(p => p.CanParse(contentType ?? string.Empty, fileName ?? string.Empty));
        return parser is not null;
    }
}
