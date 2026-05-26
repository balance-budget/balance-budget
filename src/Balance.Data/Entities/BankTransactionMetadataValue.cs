using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class BankTransactionMetadataValue
{
    public BankTransactionId BankTransactionId { get; set; }
    public BankTransactionMetadataKeyId KeyId { get; set; }

    // Populated by the extractor (which knows the key Name but not its Id);
    // the import service resolves the Name to an existing or newly-inserted
    // BankTransactionMetadataKey and writes KeyId before SaveChanges.
    public BankTransactionMetadataKey? Key { get; set; }

    public string? StringValue { get; init; }
    public long? IntegerValue { get; init; }
}
