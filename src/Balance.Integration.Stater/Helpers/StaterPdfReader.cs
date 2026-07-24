using Balance.Integration.Stater.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Balance.Integration.Stater.Helpers;

internal static class StaterPdfReader
{
    private const double LineYTolerance = 0.5;

    public static List<StaterTextLine> ExtractLines(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        using var document = PdfDocument.Open(stream);
        var lines = new List<StaterTextLine>();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines.AddRange(ExtractPageLines(page));
        }

        return lines;
    }

    // PDF origin is bottom-left, so descending Y is top-down; ascending X is left-to-right.
    private static IEnumerable<StaterTextLine> ExtractPageLines(Page page) =>
        page.GetWords()
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / LineYTolerance))
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var words = g.OrderBy(w => w.BoundingBox.Left)
                    .Select(w => new StaterWord(w.Text, w.BoundingBox.Left, w.BoundingBox.Right))
                    .ToList();
                return new StaterTextLine(string.Join(' ', words.Select(w => w.Text)), words);
            });

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
