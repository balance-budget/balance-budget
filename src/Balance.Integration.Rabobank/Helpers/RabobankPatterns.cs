using System.Text.RegularExpressions;

namespace Balance.Integration.Rabobank.Helpers;

internal static partial class RabobankPatterns
{
    // Current/savings exports are named "CSV_A_<IBAN>_<currency>_<from>_<to>.csv".
    [GeneratedRegex(@"^CSV_A_(?<iban>[A-Z]{2}\d{2}[A-Z0-9]+?)_[A-Z]{3}_", RegexOptions.IgnoreCase)]
    public static partial Regex AccountStatementFilename();

    // Credit-card exports are named "RA_CC_<last four digits>_<from>_<to>.csv".
    [GeneratedRegex(@"^RA_CC_(?<last4>\d{4})_", RegexOptions.IgnoreCase)]
    public static partial Regex CreditCardStatementFilename();

    // The masked card number inside the description of a current-account row that moves money to
    // or from the card, e.g. "****.****.****.1234" or "Creditcard .....1234".
    [GeneratedRegex(@"(?:\*{4}\.)+(?<last4>\d{4})|\.{5}(?<last4>\d{4})")]
    public static partial Regex MaskedCardNumber();

    [GeneratedRegex(@"\s+")]
    public static partial Regex Whitespace();
}
