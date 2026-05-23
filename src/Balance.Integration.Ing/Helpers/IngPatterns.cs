using System.Text.RegularExpressions;

namespace Balance.Integration.Ing.Helpers;

internal static partial class IngPatterns
{
    [GeneratedRegex(@"^(?<num>\w+)_")]
    public static partial Regex ExportAccountNumberPattern();

    [GeneratedRegex(@"(D\d{8})")]
    public static partial Regex SavingsAccountPattern();

    [GeneratedRegex(@"^[A-Z]{2}\d{2}")]
    public static partial Regex IbanPrefixPattern();
}
