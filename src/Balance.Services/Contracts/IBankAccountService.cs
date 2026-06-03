using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IBankAccountService
{
    Task<PagedOutput<BankAccountOutput>> ListAsync(
        int skip,
        int? take,
        string? search,
        BankAccountOwnerFilter? owner,
        CancellationToken cancellationToken
    );

    Task<Result<BankAccountOutput>> GetAsync(BankAccountId id, CancellationToken cancellationToken);

    Task<Result<UpdateBankAccountInput>> GetSnapshotAsync(
        BankAccountId id,
        CancellationToken cancellationToken
    );

    Task<Result<BankAccountOutput>> CreateAsync(
        CreateBankAccountInput input,
        CancellationToken cancellationToken
    );

    Task<Result<BankAccountOutput>> UpdateAsync(
        BankAccountId id,
        UpdateBankAccountInput input,
        CancellationToken cancellationToken
    );

    Task<Result> DeleteAsync(BankAccountId id, CancellationToken cancellationToken);
}

public sealed record CreateBankAccountInput(
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
    CounterpartyId? CounterpartyId
);

public sealed record UpdateBankAccountInput
{
    public BankAccountType Type { get; set; }
    public string? Iban { get; set; }
    public string? AccountNumber { get; set; }
    public string? CardIdentifier { get; set; }
    public string? Bic { get; set; }
    public string? BankName { get; set; }
    public string? AccountHolderName { get; set; }
    public CurrencyCode? CurrencyCode { get; set; }
    public string? ImporterKey { get; set; }
    public AccountId? AccountId { get; set; }
    public CounterpartyId? CounterpartyId { get; set; }
}
