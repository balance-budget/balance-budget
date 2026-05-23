using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IBankTransactionService
{
    Task<IReadOnlyList<BankTransactionOutput>> ListAsync(CancellationToken cancellationToken);

    Task<Result<BankTransactionOutput>> GetAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    );

    Task<Result<BankTransactionOutput>> CreateAsync(
        CreateBankTransactionInput input,
        CancellationToken cancellationToken
    );

    Task<Result> DeleteAsync(BankTransactionId id, CancellationToken cancellationToken);
}

public sealed record CreateBankTransactionInput(
    BankAccountId BankAccountId,
    DateOnly BookingDate,
    long Amount,
    CurrencyCode CurrencyCode
);
