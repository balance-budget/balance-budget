using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IBankTransactionService
{
    Task<PagedOutput<BankTransactionOutput>> ListAsync(
        int skip,
        int take,
        BankTransactionListFilter filter,
        string? search,
        CancellationToken cancellationToken
    );

    Task<Result<BankTransactionDetailOutput>> GetAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    );

    Task<Result<BankTransactionOutput>> CreateAsync(
        CreateBankTransactionInput input,
        CancellationToken cancellationToken
    );

    Task<Result> DeleteAsync(BankTransactionId id, CancellationToken cancellationToken);

    Task<Result<BankTransactionOutput>> DismissAsync(
        BankTransactionId id,
        string reason,
        CancellationToken cancellationToken
    );

    Task<Result<BankTransactionOutput>> UndismissAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    );
}

public sealed record CreateBankTransactionInput(
    BankAccountId BankAccountId,
    DateOnly BookingDate,
    long Amount,
    CurrencyCode CurrencyCode,
    string Description,
    string? CounterpartyName,
    string? CounterpartyAccountNumber
);
