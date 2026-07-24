using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Balance.Integration.Stater.Helpers;

internal static class StaterPdfReader
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

    // PDF origin is bottom-left, so descending Y is top-down; ascending X is left-to-right.
    private static IEnumerable<string> ExtractPageLines(Page page) =>
        page.GetWords()
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / LineYTolerance))
            .OrderByDescending(g => g.Key)
            .Select(g => string.Join(' ', g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));

    // Leaves the stream rewound.
    public static bool LooksLikePdf(Stream stream)
    {
        if (!stream.CanSeek)
            return false;

        Span<byte> header = stackalloc byte[5];
        stream.Seek(0, SeekOrigin.Begin);
        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Seek(0, SeekOrigin.Begin);
        return read == header.Length && header.SequenceEqual("%PDF-"u8);
    }
}
