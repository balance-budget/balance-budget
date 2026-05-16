using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Configurations;

internal static class CurrencySeed
{
    public static readonly IReadOnlyList<Currency> All =
    [
        new()
        {
            Code = new CurrencyCode("EUR"),
            Name = "Euro",
            MinorUnitScale = 2,
            Symbol = "€",
        },
        new()
        {
            Code = new CurrencyCode("USD"),
            Name = "United States Dollar",
            MinorUnitScale = 2,
            Symbol = "$",
        },
        new()
        {
            Code = new CurrencyCode("GBP"),
            Name = "Pound Sterling",
            MinorUnitScale = 2,
            Symbol = "£",
        },
        new()
        {
            Code = new CurrencyCode("JPY"),
            Name = "Japanese Yen",
            MinorUnitScale = 0,
            Symbol = "¥",
        },
        new()
        {
            Code = new CurrencyCode("CHF"),
            Name = "Swiss Franc",
            MinorUnitScale = 2,
            Symbol = "CHF",
        },
        new()
        {
            Code = new CurrencyCode("BTC"),
            Name = "Bitcoin",
            MinorUnitScale = 8,
            Symbol = "₿",
        },
        new()
        {
            Code = new CurrencyCode("ETH"),
            Name = "Ether",
            MinorUnitScale = 18,
            Symbol = "Ξ",
        },
    ];
}
