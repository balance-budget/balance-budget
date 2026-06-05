using Balance.Data.Entities.Enums;

namespace Balance.Services.Contracts;

/// <summary>
/// One entry in the importer registry — a stable identifier and the
/// <see cref="BankAccountType"/> the extractor accepts. The SPA's BankAccount creation form
/// uses this to filter the ImporterKey dropdown by the chosen Type (ADR 0009).
/// </summary>
public sealed record BankAccountImporterOutput(string Key, BankAccountType SupportedType);
