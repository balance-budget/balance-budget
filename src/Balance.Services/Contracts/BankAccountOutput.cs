using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record BankAccountOutput(
    BankAccountId Id,
    string? Iban,
    string? AccountNumber,
    string? Bic,
    string? BankName,
    string? AccountHolderName,
    CurrencyCode? CurrencyCode,
    AccountId? AccountId,
    CounterpartyId? CounterpartyId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
