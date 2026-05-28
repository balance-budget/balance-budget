using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record BankAccountOutput(
    BankAccountId Id,
    BankAccountType Type,
    string? Iban,
    string? AccountNumber,
    string? CardIdentifier,
    string? Bic,
    string? BankName,
    string? AccountHolderName,
    CurrencyCode? CurrencyCode,
    string? ImporterKey,
    AccountId? AccountId,
    CounterpartyId? CounterpartyId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
