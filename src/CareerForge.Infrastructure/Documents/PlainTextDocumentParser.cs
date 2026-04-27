using CareerForge.Application.Abstractions.Documents;

namespace CareerForge.Infrastructure.Documents;

/// <summary>Reads .txt / .md uploads (or any <c>text/*</c> MIME type) into a single trimmed string.</summary>
public sealed class PlainTextDocumentParser : IDocumentParser
{
    private static readonly string[] TextExtensions = { ".txt", ".md", ".text" };

    public bool CanParse(string contentType, string fileName)
    {
        if (!string.IsNullOrEmpty(contentType) && contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return true;
        var ext = Path.GetExtension(fileName);
        return TextExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ParsedDocument> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return new ParsedDocument(text.Trim(), 1);
    }
}
