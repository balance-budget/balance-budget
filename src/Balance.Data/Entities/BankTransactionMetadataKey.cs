using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class BankTransactionMetadataKey : BaseEntity<BankTransactionMetadataKeyId>
{
    public required string Name { get; init; }
}
