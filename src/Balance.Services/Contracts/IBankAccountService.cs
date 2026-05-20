using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IBankAccountService
{
    Task<IReadOnlyList<BankAccountOutput>> ListAsync(CancellationToken cancellationToken);

    Task<BankAccountOutput?> GetAsync(BankAccountId id, CancellationToken cancellationToken);

    Task<UpdateBankAccountInput?> GetSnapshotAsync(
        BankAccountId id,
        CancellationToken cancellationToken
    );

    Task<BankAccountOutput> CreateAsync(
        CreateBankAccountInput input,
        CancellationToken cancellationToken
    );

    Task<BankAccountOutput> UpdateAsync(
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

public sealed record UpdateBankAccountInput
{
    public string? Iban { get; set; }
    public string? AccountNumber { get; set; }
    public string? Bic { get; set; }
    public string? BankName { get; set; }
    public string? AccountHolderName { get; set; }
    public CurrencyCode? CurrencyCode { get; set; }
    public AccountId? AccountId { get; set; }
    public CounterpartyId? CounterpartyId { get; set; }
}
