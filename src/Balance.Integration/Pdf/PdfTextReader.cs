using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Balance.Integration.Pdf;

// PDFs carry positioned glyphs, not lines, so every layout parser needs the same first step:
// reconstruct the visual lines from PdfPig's words. This is that step, shared by all importers.
public static class PdfTextReader
{
    // Words on one line share a baseline to well under a point; rounding to this grid tolerates
    // sub-point drift without merging adjacent lines.
    private const double BaselineTolerance = 0.5;

    public static IReadOnlyList<PdfTextLine> ExtractLines(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var document = PdfDocument.Open(stream);
        var lines = new List<PdfTextLine>();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines.AddRange(ExtractPageLines(page));
        }

        return lines;
    }

    // Descending baseline is top-down; ascending Left is left-to-right within a line.
    private static IEnumerable<PdfTextLine> ExtractPageLines(Page page) =>
        page.GetWords()
            .GroupBy(word => Math.Round(word.BoundingBox.Bottom / BaselineTolerance))
            .OrderByDescending(group => group.Key)
            .Select(group =>
            {
                var words = group
                    .OrderBy(word => word.BoundingBox.Left)
                    .Select(word => new PdfWord(
                        word.Text,
                        word.BoundingBox.Left,
                        word.BoundingBox.Right,
                        word.BoundingBox.Top,
                        word.BoundingBox.Bottom
                    ))
                    .ToList();

                return new PdfTextLine(
                    page.Number,
                    group.Key * BaselineTolerance,
                    string.Join(' ', words.Select(word => word.Text)),
                    words
                );
            });

    // Cheap check that a stream is a PDF, so a detection probe can skip (rather than throw on) a
    // dropped CSV or other non-PDF file. Leaves the stream rewound.
    public static bool LooksLikePdf(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
            return false;

        Span<byte> header = stackalloc byte[5];
        stream.Seek(0, SeekOrigin.Begin);
        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Seek(0, SeekOrigin.Begin);
        return read == header.Length && header.SequenceEqual("%PDF-"u8);
    }
}
