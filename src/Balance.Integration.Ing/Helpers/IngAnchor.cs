namespace Balance.Integration.Ing.Helpers;

// Account-anchor helpers shared by the ING extractors' detection probes (ADR 0034). Normalization
// matches what ExtractAsync compares against (spaces stripped, upper-cased), so an anchor resolved
// here lines up with the same identifier the import re-validates.
internal static class IngAnchor
{
    public static string Normalize(string? value) =>
        value is null
            ? string.Empty
            : value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    // ING current/savings exports embed the account IBAN at the start of the filename — the
    // fastest reliable anchor. Returns null when the name does not carry one (the probe then
    // falls back to reading the file content).
    public static string? FromFilename(string fileName)
    {
        var match = IngPatterns.StatementFilenameIban().Match(fileName);
        return match.Success ? Normalize(match.Groups["iban"].Value) : null;
    }

    // Cheap, allocation-light check that a stream is a PDF, so the credit-card probe can skip
    // (rather than throw on) a dropped CSV or other non-PDF file. Leaves the stream rewound.
    public static bool LooksLikePdf(Stream stream)
    {
        if (!stream.CanSeek)
            return false;

        stream.Seek(0, SeekOrigin.Begin);
        Span<byte> header = stackalloc byte[5];
        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Seek(0, SeekOrigin.Begin);
        return read == header.Length && header.SequenceEqual("%PDF-"u8);
    }
}
