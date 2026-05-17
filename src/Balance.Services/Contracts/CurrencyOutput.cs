using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record CurrencyOutput(
    CurrencyCode Code,
    string Name,
    int MinorUnitScale,
    string? Symbol
);
