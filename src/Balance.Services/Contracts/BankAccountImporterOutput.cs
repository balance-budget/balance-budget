using Balance.Data.Entities.Enums;

namespace Balance.Services.Contracts;

/// <summary>
/// One entry in the importer registry — a stable identifier, the bank's proper-noun name, and
/// the <see cref="BankAccountType"/> the extractor accepts. The SPA's BankAccount creation form
/// uses this to filter the ImporterKey dropdown by the chosen Type and to render a friendly
/// label (<c>BankName</c> + the translated type word) instead of the raw key (ADR 0009/0034).
/// </summary>
public sealed record BankAccountImporterOutput(
    string Key,
    string BankName,
    BankAccountType SupportedType
);
