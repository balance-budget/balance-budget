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
    /// Parses a currency-amount note value of the form <c>"100,00 BYN"</c>. Returns
    /// <c>null</c> when the amount is non-numeric or the currency code is missing —
    /// keeps a single malformed ING row from sinking a whole import.
    /// </summary>
    internal static CurrencyAmount? TryParse(string value)
    {
        var parts = value.Split(' ', 2);
        if (parts.Length < 2)
            return null;

        if (
            !decimal.TryParse(
                parts[0],
                NumberStyles.Number,
                CultureInfo.GetCultureInfo("nl-NL"),
                out var amount
            )
        )
            return null;

        return new CurrencyAmount(amount, parts[1]);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount} {CurrencyCode}");
}
