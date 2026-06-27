using System.Text.RegularExpressions;

namespace Balance.Integration.Ing.Helpers;

internal static partial class IngPatterns
{
    [GeneratedRegex(@"([A-Z]\d{8})")]
    public static partial Regex SavingsAccount();

    // ING current/savings exports are named "<IBAN>_<from>_<to>.csv" (e.g.
    // "NL69INGB0123456789_01-01-2026_31-01-2026.csv"), so the leading IBAN is the fastest
    // account anchor for drop-and-detect (ADR 0034). Anchored to the start so it never matches
    // an IBAN buried elsewhere in the name.
    [GeneratedRegex(@"^(?<iban>NL\d{2}INGB\d{10})_", RegexOptions.IgnoreCase)]
    public static partial Regex StatementFilenameIban();

    [GeneratedRegex(@"^NL\d{2} INGB \d{4} \d{4} \d{2}$", RegexOptions.IgnoreCase)]
    public static partial Regex CreditCardLinkedAccount();

    [GeneratedRegex(
        @"^(?<date>\d{2}-\d{2}-\d{4})\s+(?<name>.+?)\s+(?<type>Betaling|Ontvangst|Incasso|Aflossing|Geldopname|Kosten|Correctie)\s+(?<amount>[+-]\s*\d[\d.,]*\d{2})$"
    )]
    public static partial Regex ModernCreditCardTransactionLine();

    [GeneratedRegex(
        @"^(Transactiedatum: (?<date>\d{2}-\d{2}-\d{4})|Kaartnummer: (?<cardno>\d{4} \*{4} \*{4} \d{4})|Bedrag: (?<fcamount>\d+\,\d+) (?<fccode>[A-Z]{3})|Koers: (?<fcrate>\d+\,\d+)|Koersopslag \((?<fcmarkupcode>[A-Z]{3})\): (?<fcmarkupamount>\d+\,\d+))$",
        RegexOptions.IgnoreCase
    )]
    public static partial Regex ModernCreditCardNoteLine();

    [GeneratedRegex(
        @"Geboekt op Naam|Overeenkomstnummer|Dit product valt|Op ING\.nl",
        RegexOptions.IgnoreCase
    )]
    public static partial Regex ModernCreditCardFooter();

    [GeneratedRegex(@"^(?<date>\d{2}-\d{2}-\d{4})\s+(\d+,\d+)$")]
    public static partial Regex LegacyCreditCardRepaymentDateLine();

    [GeneratedRegex(@"^KAARTNUMMER (?<cardno>\d{4}\.\*{4}\.\*{4}\.\d{4})$")]
    public static partial Regex LegacyCreditCardNumberLine();

    [GeneratedRegex(
        @"^((?<date>\d{2} [a-z]{3})\s+)*(?<desc>.+?)\s+(?<fc>(?<fcamount>\d+,\d+)\s+(?<fccode>[A-Z]{3})\s+(?<fcrate>\d+,\d+)\s+)*(?<dc>AF|BIJ)\s+(?<amount>\d+,\d+)$"
    )]
    public static partial Regex LegacyCreditCardTransactionLine();
}
