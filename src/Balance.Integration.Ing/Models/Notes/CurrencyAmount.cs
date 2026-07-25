using System.Globalization;

namespace Balance.Integration.Ing.Models.Notes;

internal sealed class CurrencyAmount
{
    public decimal Amount { get; }
    public string CurrencyCode { get; }

    public CurrencyAmount(decimal amount, string currencyCode)
    {
        Amount = amount;
        CurrencyCode = currencyCode;
    }

    /// <summary>
    /// Parses a currency-amount note value of the form <c>"100,00 BYN"</c> under the layout's own
    /// number culture (the English credit-card CSV export writes <c>"17,423,400.00 IDR"</c> for the
    /// same value the Dutch one writes as <c>"17.423.400,00 IDR"</c>). Returns <c>null</c> when the
    /// amount is non-numeric or the currency code is missing — keeps a single malformed ING row from
    /// sinking a whole import.
    /// </summary>
    internal static CurrencyAmount? TryParse(string value, CultureInfo culture)
    {
        var parts = value.Split(' ', 2);
        if (parts.Length < 2)
            return null;

        if (!decimal.TryParse(parts[0], NumberStyles.Number, culture, out var amount))
            return null;

        return new CurrencyAmount(amount, parts[1]);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount} {CurrencyCode}");
}
