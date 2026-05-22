using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.BankAccounts;

internal sealed class BankAccountService : IBankAccountService
{
    private readonly BalanceDbContext _dbContext;
    private readonly ICurrencyService _currencyService;
    private readonly TimeProvider _timeProvider;

    public BankAccountService(
        BalanceDbContext dbContext,
        ICurrencyService currencyService,
        TimeProvider timeProvider
    )
    {
        _dbContext = dbContext;
        _currencyService = currencyService;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<BankAccountOutput>> ListAsync(
        CancellationToken cancellationToken
    ) =>
        await _dbContext
            .BankAccounts.OrderBy(b => b.CreatedAt)
            .Select(b => new BankAccountOutput(
                b.Id,
                b.Iban,
                b.AccountNumber,
                b.Bic,
                b.BankName,
                b.AccountHolderName,
                b.CurrencyCode,
                b.AccountId,
                b.CounterpartyId,
                b.CreatedAt,
                b.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

    public Task<BankAccountOutput?> GetAsync(
        BankAccountId id,
        CancellationToken cancellationToken
    ) =>
        _dbContext
            .BankAccounts.Where(b => b.Id == id)
            .Select(b => new BankAccountOutput(
                b.Id,
                b.Iban,
                b.AccountNumber,
                b.Bic,
                b.BankName,
                b.AccountHolderName,
                b.CurrencyCode,
                b.AccountId,
                b.CounterpartyId,
                b.CreatedAt,
                b.UpdatedAt
            ))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<UpdateBankAccountInput?> GetSnapshotAsync(
        BankAccountId id,
        CancellationToken cancellationToken
    ) =>
        _dbContext
            .BankAccounts.AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new UpdateBankAccountInput
            {
                Iban = b.Iban,
                AccountNumber = b.AccountNumber,
                Bic = b.Bic,
                BankName = b.BankName,
                AccountHolderName = b.AccountHolderName,
                CurrencyCode = b.CurrencyCode,
                AccountId = b.AccountId,
                CounterpartyId = b.CounterpartyId,
            })
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Result<BankAccountOutput>> CreateAsync(
        CreateBankAccountInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var iban = input.Iban.TrimToNull();
        var accountNumber = input.AccountNumber.TrimToNull();
        var bic = input.Bic.TrimToNull();
        var bankName = input.BankName.TrimToNull();
        var accountHolderName = input.AccountHolderName.TrimToNull();

        if (EnsureOwnershipXor(input.AccountId, input.CounterpartyId) is { Error: { } e1 })
        {
            return e1;
        }
        if (EnsureIbanOrAccountNumber(iban, accountNumber) is { Error: { } e2 })
        {
            return e2;
        }

        if (
            await EnsureReferencedRowsExistAsync(
                input.CurrencyCode,
                input.AccountId,
                input.CounterpartyId,
                cancellationToken
            ) is
            { Error: { } e3 }
        )
        {
            return e3;
        }

        if (
            await EnsureIbanAvailableAsync(iban, excludingId: null, cancellationToken) is
            { Error: { } e4 }
        )
        {
            return e4;
        }
        if (
            await EnsureAccountSlotAvailableAsync(
                input.AccountId,
                excludingId: null,
                cancellationToken
            ) is
            { Error: { } e5 }
        )
        {
            return e5;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var bankAccount = new BankAccount
        {
            Id = new BankAccountId(Guid.CreateVersion7()),
            Iban = iban,
            AccountNumber = accountNumber,
            Bic = bic,
            BankName = bankName,
            AccountHolderName = accountHolderName,
            CurrencyCode = input.CurrencyCode,
            AccountId = input.AccountId,
            CounterpartyId = input.CounterpartyId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _dbContext.BankAccounts.Add(bankAccount);
        if (await _dbContext.SaveChangesAndCatchAsync(cancellationToken) is { Error: { } e6 })
        {
            return e6;
        }
        return ToOutput(bankAccount);
    }

    public async Task<Result<BankAccountOutput>> UpdateAsync(
        BankAccountId id,
        UpdateBankAccountInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var bankAccount = await _dbContext.BankAccounts.FirstOrDefaultAsync(
            b => b.Id == id,
            cancellationToken
        );
        if (bankAccount is null)
        {
            return new NotFoundError("BankAccount", id.Value.ToString());
        }

        var iban = input.Iban.TrimToNull();
        var accountNumber = input.AccountNumber.TrimToNull();
        var bic = input.Bic.TrimToNull();
        var bankName = input.BankName.TrimToNull();
        var accountHolderName = input.AccountHolderName.TrimToNull();

        if (EnsureOwnershipXor(input.AccountId, input.CounterpartyId) is { Error: { } e1 })
        {
            return e1;
        }
        if (EnsureIbanOrAccountNumber(iban, accountNumber) is { Error: { } e2 })
        {
            return e2;
        }

        if (
            await EnsureReferencedRowsExistAsync(
                input.CurrencyCode,
                input.AccountId,
                input.CounterpartyId,
                cancellationToken
            ) is
            { Error: { } e3 }
        )
        {
            return e3;
        }

        if (
            await EnsureIbanAvailableAsync(iban, excludingId: id, cancellationToken) is
            { Error: { } e4 }
        )
        {
            return e4;
        }
        if (
            await EnsureAccountSlotAvailableAsync(
                input.AccountId,
                excludingId: id,
                cancellationToken
            ) is
            { Error: { } e5 }
        )
        {
            return e5;
        }

        bankAccount.Iban = iban;
        bankAccount.AccountNumber = accountNumber;
        bankAccount.Bic = bic;
        bankAccount.BankName = bankName;
        bankAccount.AccountHolderName = accountHolderName;
        bankAccount.CurrencyCode = input.CurrencyCode;
        bankAccount.AccountId = input.AccountId;
        bankAccount.CounterpartyId = input.CounterpartyId;
        bankAccount.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        if (await _dbContext.SaveChangesAndCatchAsync(cancellationToken) is { Error: { } e6 })
        {
            return e6;
        }
        return ToOutput(bankAccount);
    }

    public async Task<Result> DeleteAsync(BankAccountId id, CancellationToken cancellationToken)
    {
        var result = await _dbContext
            .BankAccounts.Where(c => c.Id == id)
            .ExecuteDeleteAndCatchAsync(cancellationToken);

        if (result.IsFailure)
            return result.Error;

        if (result.Value == 0)
            return new NotFoundError("BankAccount", id.Value.ToString());

        return Result.Success;
    }

    private static BankAccountOutput ToOutput(BankAccount bankAccount) =>
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

    private static Result EnsureOwnershipXor(AccountId? accountId, CounterpartyId? counterpartyId)
    {
        var hasAccount = accountId.HasValue;
        var hasCounterparty = counterpartyId.HasValue;
        if (hasAccount == hasCounterparty)
        {
            return new InvariantError(
                ErrorCodes.BankAccountOwnershipXor,
                "A BankAccount must reference exactly one of AccountId or CounterpartyId."
            );
        }
        return Result.Success;
    }

    private static Result EnsureIbanOrAccountNumber(string? iban, string? accountNumber)
    {
        if (iban is null && accountNumber is null)
        {
            return new InvariantError(
                ErrorCodes.BankAccountIdentifierMissing,
                "A BankAccount must have at least one of Iban or AccountNumber."
            );
        }
        return Result.Success;
    }

    private async Task<Result> EnsureReferencedRowsExistAsync(
        CurrencyCode? currencyCode,
        AccountId? accountId,
        CounterpartyId? counterpartyId,
        CancellationToken cancellationToken
    )
    {
        if (currencyCode is { } code)
        {
            if (await _currencyService.GetAsync(code, cancellationToken) is null)
            {
                return new NotFoundError("Currency", code.Value);
            }
        }

        if (accountId is { } aid)
        {
            var exists = await _dbContext.Accounts.AnyAsync(a => a.Id == aid, cancellationToken);
            if (!exists)
            {
                return new NotFoundError("Account", aid.Value.ToString());
            }
        }

        if (counterpartyId is { } cid)
        {
            var exists = await _dbContext.Counterparties.AnyAsync(
                c => c.Id == cid,
                cancellationToken
            );
            if (!exists)
            {
                return new NotFoundError("Counterparty", cid.Value.ToString());
            }
        }
        return Result.Success;
    }

    private async Task<Result> EnsureIbanAvailableAsync(
        string? iban,
        BankAccountId? excludingId,
        CancellationToken cancellationToken
    )
    {
        if (iban is null)
        {
            return Result.Success;
        }

        var taken = await _dbContext.BankAccounts.AnyAsync(
            b => b.Iban == iban && (excludingId == null || b.Id != excludingId),
            cancellationToken
        );
        if (taken)
        {
            return new ConflictError(
                ErrorCodes.BankAccountIbanTaken,
                $"A BankAccount with IBAN '{iban}' already exists."
            );
        }
        return Result.Success;
    }

    private async Task<Result> EnsureAccountSlotAvailableAsync(
        AccountId? accountId,
        BankAccountId? excludingId,
        CancellationToken cancellationToken
    )
    {
        if (accountId is null)
        {
            return Result.Success;
        }

        var taken = await _dbContext.BankAccounts.AnyAsync(
            b => b.AccountId == accountId && (excludingId == null || b.Id != excludingId),
            cancellationToken
        );
        if (taken)
        {
            return new ConflictError(
                ErrorCodes.BankAccountSlotTaken,
                "A BankAccount for that Account already exists."
            );
        }
        return Result.Success;
    }
}
