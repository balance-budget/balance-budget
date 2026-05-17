using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IBankTransactionService
{
    Task<IReadOnlyList<BankTransaction>> ListAsync(CancellationToken cancellationToken);

    Task<BankTransaction?> GetAsync(BankTransactionId id, CancellationToken cancellationToken);

    Task<BankTransaction> CreateAsync(
        CreateBankTransactionInput input,
        CancellationToken cancellationToken
    );

    Task DeleteAsync(BankTransactionId id, CancellationToken cancellationToken);
}

public sealed record CreateBankTransactionInput(
    BankAccountId BankAccountId,
    DateOnly BookingDate,
    long Amount,
    CurrencyCode CurrencyCode
);
