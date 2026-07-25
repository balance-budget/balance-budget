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

    // ING credit-card CSV exports are named "CreditCard_<Overeenkomstnummer>_<from>_<to>.csv". That
    // contract number is not the masked card number, so it only resolves a BankAccount that records
    // it as its AccountNumber — which is why it is a fallback for statements whose every row is a
    // Funding-account transfer and therefore names no card at all (ADR 0038).
    public static string? FromCreditCardFilename(string fileName)
    {
        var match = IngPatterns.CreditCardFilenameContractNumber().Match(fileName);
        return match.Success ? Normalize(match.Groups["number"].Value) : null;
    }
}
