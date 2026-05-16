using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class Currency
{
    public required CurrencyCode Code { get; init; }
    public required string Name { get; set; }
    public required int MinorUnitScale { get; set; }
    public string? Symbol { get; set; }
}
