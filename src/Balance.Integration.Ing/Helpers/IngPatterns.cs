using System.Text.RegularExpressions;

namespace Balance.Integration.Ing.Helpers;

internal static partial class IngPatterns
{
    [GeneratedRegex(@"(D\d{8})")]
    public static partial Regex SavingsAccount();

    [GeneratedRegex(
        @"^(?<date>\d{2}-\d{2}-\d{4})\s+(?<name>.+?)\s+(?<type>Betaling|Ontvangst|Incasso|Geldopname|Kosten|Correctie)\s+(?<amount>[+-]\s*\d[\d.,]*\d{2})$"
    )]
    public static partial Regex CreditCardTransactionLine();

    [GeneratedRegex(
        @"^(Transactiedatum: (?<date>\d{2}-\d{2}-\d{4})|Kaartnummer: (?<cardno>\d{4} \*\*\*\* \*\*\*\* \d{4})|Bedrag: (?<fcamount>\d+\,\d+) (?<fccode>[A-Z]{3})|Koers: (?<fcrate>\d+\,\d+)|Koersopslag \((?<fcmarkupcode>[A-Z]{3})\): (?<fcmarkupamount>\d+\,\d+))$",
        RegexOptions.IgnoreCase
    )]
    public static partial Regex CreditCardNoteLine();

    [GeneratedRegex(
        @"Geboekt op Naam|Overeenkomstnummer|Dit product valt|Op ING\.nl",
        RegexOptions.IgnoreCase
    )]
    public static partial Regex CreditCardFooter();
}
