using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Configurations;

internal static class CurrencySeed
{
    // EUR only for now — anything else is user-added via the currencies API.
    public static readonly IReadOnlyList<Currency> All =
    [
        new()
        {
            Code = new CurrencyCode("EUR"),
            Name = "Euro",
            MinorUnitScale = 2,
            Symbol = "€",
        },
    ];
}
