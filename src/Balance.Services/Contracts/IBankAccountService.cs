using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IBankAccountService
{
    Task<IReadOnlyList<BankAccount>> ListAsync(CancellationToken cancellationToken);

    Task<BankAccount?> GetAsync(BankAccountId id, CancellationToken cancellationToken);

    Task<BankAccount> CreateAsync(
        CreateBankAccountInput input,
        CancellationToken cancellationToken
    );

    Task<BankAccount> UpdateAsync(
        BankAccountId id,
        UpdateBankAccountInput input,
        CancellationToken cancellationToken
    );

    Task DeleteAsync(BankAccountId id, CancellationToken cancellationToken);
}

public sealed record CreateBankAccountInput(
    string? Iban,
    string? AccountNumber,
    string? Bic,
    string? BankName,
    string? AccountHolderName,
    CurrencyCode? CurrencyCode,
    AccountId? AccountId,
    CounterpartyId? CounterpartyId
);

public sealed record UpdateBankAccountInput(
    string? Iban,
    string? AccountNumber,
    string? Bic,
    string? BankName,
    string? AccountHolderName,
    CurrencyCode? CurrencyCode,
    AccountId? AccountId,
    CounterpartyId? CounterpartyId
);
