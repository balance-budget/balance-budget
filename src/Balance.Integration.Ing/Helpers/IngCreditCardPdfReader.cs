using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Balance.Integration.Ing.Helpers;

// Reconstructs the visual lines of an ING credit-card statement PDF from its positioned words.
// Shared by the layout parsers and the content-sniffing extractor so the PDF is read once and
// the concrete layout (legacy vs modern) is then resolved from the extracted lines (ADR 0034).
internal static class IngCreditCardPdfReader
{
    private const double LineYTolerance = 0.5;

    public static List<string> ExtractLines(Stream stream, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(stream);
        var lines = new List<string>();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines.AddRange(ExtractPageLines(page));
        }

        return lines;
    }

    // Group words by Y (with tolerance) to reconstruct visual lines. PDF coordinates put
    // the origin at the bottom-left, so larger Y is higher on the page — order descending
    // for top-down, then ascending X within each line for left-to-right.
    private static IEnumerable<string> ExtractPageLines(Page page) =>
        page.GetWords()
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / LineYTolerance))
            .OrderByDescending(g => g.Key)
            .Select(g => string.Join(' ', g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
}
