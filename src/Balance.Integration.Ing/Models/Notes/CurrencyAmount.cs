using System.Globalization;

namespace Balance.Integration.Ing.Models.Notes;

internal sealed class CurrencyAmount
{
    public decimal Amount { get; }
    public string CurrencyCode { get; }

    private CurrencyAmount(decimal amount, string currencyCode)
    {
        Amount = amount;
        CurrencyCode = currencyCode;
    }

    internal static CurrencyAmount Parse(string value)
    {
        var parts = value.Split(' ', 2);
        var amount = decimal.Parse(parts[0], CultureInfo.GetCultureInfo("nl-NL"));
        var currencyCode = parts[1];
        return new CurrencyAmount(amount, currencyCode);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount} {CurrencyCode}");
}
