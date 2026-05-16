using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Web.Endpoints.BankAccounts;

internal sealed record BankAccountResponse(
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
)
{
    public static BankAccountResponse From(BankAccount bankAccount) =>
        new(
            bankAccount.Id,
            bankAccount.Iban,
            bankAccount.AccountNumber,
            bankAccount.Bic,
            bankAccount.BankName,
            bankAccount.AccountHolderName,
            bankAccount.CurrencyCode,
            bankAccount.AccountId,
            bankAccount.CounterpartyId,
            bankAccount.CreatedAt,
            bankAccount.UpdatedAt
        );
}
