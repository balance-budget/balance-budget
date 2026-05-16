using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Web.Endpoints.Currencies;

internal sealed record CurrencyResponse(
    CurrencyCode Code,
    string Name,
    int MinorUnitScale,
    string? Symbol
)
{
    public static CurrencyResponse From(Currency currency) =>
        new(currency.Code, currency.Name, currency.MinorUnitScale, currency.Symbol);
}
