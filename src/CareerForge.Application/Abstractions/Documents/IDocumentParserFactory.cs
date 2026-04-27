namespace CareerForge.Application.Abstractions.Documents;

/// <summary>
/// Resolves an <see cref="IDocumentParser"/> for an uploaded file. <see cref="Resolve"/> throws
/// when nothing can handle it; <see cref="TryResolve"/> reports the same condition without throwing.
/// </summary>
public interface IDocumentParserFactory
{
    IDocumentParser Resolve(string contentType, string fileName);
    bool TryResolve(string contentType, string fileName, out IDocumentParser? parser);
}
