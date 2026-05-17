using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record CurrencyOutput(
    CurrencyCode Code,
    string Name,
    int MinorUnitScale,
    string? Symbol
)
{
    public static CurrencyOutput FromEntity(Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return new CurrencyOutput(
            currency.Code,
            currency.Name,
            currency.MinorUnitScale,
            currency.Symbol
        );
    }
}

public sealed record CreateCurrencyInput(
    CurrencyCode Code,
    string Name,
    int MinorUnitScale,
    string? Symbol
);

public sealed record UpdateCurrencyInput(string? Name, string? Symbol);
