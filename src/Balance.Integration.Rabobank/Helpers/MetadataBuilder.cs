using Balance.Data.Entities;

namespace Balance.Integration.Rabobank.Helpers;

// Collects BankTransactionMetadata entries (ADR 0009). Blank values are skipped so a key only
// exists when the statement row actually carried it.
internal sealed class MetadataBuilder
{
    private readonly List<BankTransactionMetadataValue> _entries = [];

    public MetadataBuilder AddString(string keyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _entries.Add(
                new BankTransactionMetadataValue
                {
                    Key = new BankTransactionMetadataKey { Name = keyName },
                    StringValue = value.Trim(),
                }
            );
        }
        return this;
    }

    public MetadataBuilder AddInteger(string keyName, long? value)
    {
        if (value is not null)
        {
            _entries.Add(
                new BankTransactionMetadataValue
                {
                    Key = new BankTransactionMetadataKey { Name = keyName },
                    IntegerValue = value,
                }
            );
        }
        return this;
    }

    public List<BankTransactionMetadataValue> Build() => _entries;
}
