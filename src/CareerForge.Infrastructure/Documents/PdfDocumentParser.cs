using System.Text;
using CareerForge.Application.Abstractions.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace CareerForge.Infrastructure.Documents;

/// <summary>
/// Extracts text from PDF uploads using layout-aware analysis: nearest-neighbour word
/// grouping, Docstrum bounding-box page segmentation, and unsupervised reading-order
/// detection. This preserves the intended reading flow of multi-column documents.
/// Falls back to PdfPig's content-order extractor if the layout pipeline fails or
/// produces no output.
/// </summary>
public sealed class PdfDocumentParser : IDocumentParser
{
    private static readonly string[] PdfExtensions = { ".pdf" };

    public bool CanParse(string contentType, string fileName)
    {
        if (!string.IsNullOrEmpty(contentType) && contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            return true;
        var ext = Path.GetExtension(fileName);
        return PdfExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    public Task<ParsedDocument> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var document = PdfDocument.Open(ms);
        var sb = new StringBuilder();
        var pageCount = 0;

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            pageCount++;
            sb.AppendLine(ExtractPageText(page));
            sb.AppendLine();
        }

        return Task.FromResult(new ParsedDocument(sb.ToString().Trim(), pageCount));
    }

    private static string ExtractPageText(Page page)
    {
        try
        {
            var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters);
            var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
            var ordered = UnsupervisedReadingOrderDetector.Instance.Get(blocks);
            var sb = new StringBuilder();
            foreach (var block in ordered)
            {
                sb.AppendLine(block.Text);
                sb.AppendLine();
            }
            var layoutText = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(layoutText)) return layoutText;
        }
        catch
        {
            // Layout analysis can fail on encrypted or non-standard documents; fall back
            // to the simpler content-order extractor.
        }
        return ContentOrderTextExtractor.GetText(page);
    }
}
