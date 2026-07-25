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
}
