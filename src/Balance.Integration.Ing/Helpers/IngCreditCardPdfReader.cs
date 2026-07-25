using Balance.Integration.Pdf;

namespace Balance.Integration.Ing.Helpers;

// ING's credit-card layouts are line-oriented, so the parsers only need the reconstructed text.
// Shared by the layout parsers and the content-sniffing extractor so the PDF is read once and the
// concrete layout (legacy vs modern) is then resolved from the extracted lines (ADR 0034).
internal static class IngCreditCardPdfReader
{
    public static List<string> ExtractLines(Stream stream, CancellationToken cancellationToken) =>
        [.. PdfTextReader.ExtractLines(stream, cancellationToken).Select(line => line.Text)];
}
