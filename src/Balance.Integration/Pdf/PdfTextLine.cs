namespace Balance.Integration.Pdf;

// A visual line: the words that share a baseline, joined left-to-right into Text (for anchor and
// section matching) and kept positioned in Words (for column-band bucketing).
public sealed record PdfTextLine(
    int PageNumber,
    double Baseline,
    string Text,
    IReadOnlyList<PdfWord> Words
)
{
    // Tallest word on the line: the natural unit for judging whether the next line is a wrap of
    // this one or the start of a new block.
    public double Height => Words.Count == 0 ? 0 : Words.Max(word => word.Height);
}
