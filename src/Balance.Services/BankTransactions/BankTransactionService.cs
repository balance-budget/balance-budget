using Balance.Data;
using Balance.Data.Currencies;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.BankTransactions;

internal sealed class BankTransactionService : IBankTransactionService
{
    private readonly BalanceDbContext _dbContext;
    private readonly ICurrencyLookup _currencyLookup;
    private readonly TimeProvider _timeProvider;

    public BankTransactionService(
        BalanceDbContext dbContext,
        ICurrencyLookup currencyLookup,
        TimeProvider timeProvider
    )
    {
        _dbContext = dbContext;
        _currencyLookup = currencyLookup;
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

    public Task<BankTransactionOutput?> GetAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    ) =>
        _dbContext
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

    public async Task<BankTransactionOutput> CreateAsync(
        CreateBankTransactionInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Amount == 0)
        {
            throw new DomainException(
                DomainExceptionKind.Validation,
                "BankTransaction Amount must be non-zero."
            );
        }

        if (_currencyLookup.TryGetByCode(input.CurrencyCode) is null)
        {
            throw new DomainException(
                DomainExceptionKind.NotFound,
                $"Currency '{input.CurrencyCode.Value}' is not defined."
            );
        }

        var bankAccount = await _dbContext
            .BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == input.BankAccountId, cancellationToken);
        if (bankAccount is null)
        {
            throw new DomainException(
                DomainExceptionKind.NotFound,
                $"BankAccount {input.BankAccountId} not found."
            );
        }

        if (bankAccount.AccountId is null)
        {
            throw new DomainException(
                DomainExceptionKind.Invariant,
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
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToOutput(bankTransaction);
    }

    public async Task DeleteAsync(BankTransactionId id, CancellationToken cancellationToken)
    {
        var bankTransaction =
            await _dbContext.BankTransactions.FirstOrDefaultAsync(
                b => b.Id == id,
                cancellationToken
            )
            ?? throw new DomainException(
                DomainExceptionKind.NotFound,
                $"BankTransaction {id} not found."
            );

        _dbContext.BankTransactions.Remove(bankTransaction);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new DomainException(
                DomainExceptionKind.Conflict,
                $"BankTransaction {id} is referenced by other records and cannot be deleted.",
                ex
            );
        }
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
