namespace Balance.Integration.Rabobank.Helpers;

// Account-anchor helpers shared by the Rabobank extractors' detection probes (ADR 0034).
// Normalization matches what ExtractAsync compares against (spaces stripped, upper-cased).
internal static class RabobankAnchor
{
    public static string Normalize(string? value) =>
        value is null
            ? string.Empty
            : value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    // Rabobank reveals only the last four digits of a card; they are padded to a 16-character mask
    // so the CardIdentifier reads like a masked card number.
    public static string CardIdentifier(string lastFour) =>
        string.Concat(new string('*', 12), lastFour.Trim());

    public static string? FromAccountFilename(string fileName)
    {
        var match = RabobankPatterns.AccountStatementFilename().Match(fileName);
        return match.Success ? Normalize(match.Groups["iban"].Value) : null;
    }

    public static string? FromCreditCardFilename(string fileName)
    {
        var match = RabobankPatterns.CreditCardStatementFilename().Match(fileName);
        return match.Success ? CardIdentifier(match.Groups["last4"].Value) : null;
    }
}
