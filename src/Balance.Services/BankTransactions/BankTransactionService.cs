using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.BankTransactions;

internal sealed class BankTransactionService : IBankTransactionService
{
    private readonly BalanceDbContext _dbContext;
    private readonly ICurrencyService _currencyService;
    private readonly TimeProvider _timeProvider;

    public BankTransactionService(
        BalanceDbContext dbContext,
        ICurrencyService currencyService,
        TimeProvider timeProvider
    )
    {
        _dbContext = dbContext;
        _currencyService = currencyService;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<BankTransactionOutput>> ListAsync(
        CancellationToken cancellationToken
    ) =>
        await _dbContext
            .BankTransactions.OrderByDescending(b => b.BookingDate)
            .ThenBy(b => b.CreatedAt)
            .Select(b => new BankTransactionOutput(
                b.Id,
                b.BankAccountId,
                b.BookingDate,
                b.Money,
                b.CreatedAt,
                b.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

    public async Task<Result<BankTransactionOutput>> GetAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    )
    {
        var output = await _dbContext
            .BankTransactions.Where(b => b.Id == id)
            .Select(b => new BankTransactionOutput(
                b.Id,
                b.BankAccountId,
                b.BookingDate,
                b.Money,
                b.CreatedAt,
                b.UpdatedAt
            ))
            .FirstOrDefaultAsync(cancellationToken);
        return output is null ? new NotFoundError("BankTransaction", id.Value.ToString()) : output;
    }

    public async Task<Result<BankTransactionOutput>> CreateAsync(
        CreateBankTransactionInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Amount == 0)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionAmountZero,
                "BankTransaction Amount must be non-zero."
            );
        }

        var currency = await _currencyService.GetAsync(input.CurrencyCode, cancellationToken);
        if (currency.IsFailure)
        {
            return currency.Error;
        }

        var bankAccount = await _dbContext
            .BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == input.BankAccountId, cancellationToken);
        if (bankAccount is null)
        {
            return new NotFoundError("BankAccount", input.BankAccountId.Value.ToString());
        }

        if (bankAccount.AccountId is null)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionRequiresOwnAccount,
                "BankTransactions can only be created on one of your own BankAccounts "
                    + "(BankAccount.AccountId must be set)."
            );
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var bankTransaction = new BankTransaction
        {
            Id = new BankTransactionId(Guid.CreateVersion7()),
            BankAccountId = input.BankAccountId,
            BookingDate = input.BookingDate,
            Money = new Money(input.Amount, input.CurrencyCode),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _dbContext.BankTransactions.Add(bankTransaction);
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return ToOutput(bankTransaction);
    }

    public async Task<Result> DeleteAsync(BankTransactionId id, CancellationToken cancellationToken)
    {
        var result = await _dbContext
            .BankTransactions.Where(c => c.Id == id)
            .ExecuteDeleteAndCatchAsync(cancellationToken);

        if (result.IsFailure)
            return result.Error;

        if (result.Value == 0)
            return new NotFoundError("BankTransaction", id.Value.ToString());

        return Result.Success;
    }

    private static BankTransactionOutput ToOutput(BankTransaction bankTransaction) =>
        new(
            bankTransaction.Id,
            bankTransaction.BankAccountId,
            bankTransaction.BookingDate,
            bankTransaction.Money,
            bankTransaction.CreatedAt,
            bankTransaction.UpdatedAt
        );
}
