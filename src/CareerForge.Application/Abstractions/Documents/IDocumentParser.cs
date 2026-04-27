namespace CareerForge.Application.Abstractions.Documents;

/// <summary>Extracts plain text from an uploaded document of a particular MIME / extension.</summary>
public interface IDocumentParser
{
    bool CanParse(string contentType, string fileName);
    Task<ParsedDocument> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}

/// <summary>Result of parsing a document into plain text.</summary>
public sealed record ParsedDocument(string Text, int PageCount);
