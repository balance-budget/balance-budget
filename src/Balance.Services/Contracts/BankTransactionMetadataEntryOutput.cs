namespace Balance.Services.Contracts;

public sealed record BankTransactionMetadataEntryOutput(
    string Key,
    string? StringValue,
    long? IntegerValue
);
