using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class Counterparty : BaseEntity<CounterpartyId>
{
    public required string Name { get; set; }
}
