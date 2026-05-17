using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Data.Helpers;
using Balance.Services.Contracts;
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

    public async Task<BankAccountOutput> CreateAsync(
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

        EnsureOwnershipXor(input.AccountId, input.CounterpartyId);
        EnsureIbanOrAccountNumber(iban, accountNumber);

        await EnsureReferencedRowsExistAsync(
            input.CurrencyCode,
            input.AccountId,
            input.CounterpartyId,
            cancellationToken
        );

        await EnsureIbanAvailableAsync(iban, excludingId: null, cancellationToken);
        await EnsureAccountSlotAvailableAsync(
            input.AccountId,
            excludingId: null,
            cancellationToken
        );

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
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToOutput(bankAccount);
    }

    public async Task<BankAccountOutput> UpdateAsync(
        BankAccountId id,
        UpdateBankAccountInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var bankAccount =
            await _dbContext.BankAccounts.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new DomainException(
                DomainExceptionKind.NotFound,
                $"BankAccount {id} not found."
            );

        if (input.Iban is not null)
            bankAccount.Iban = input.Iban.TrimToNull();
        if (input.AccountNumber is not null)
            bankAccount.AccountNumber = input.AccountNumber.TrimToNull();
        if (input.Bic is not null)
            bankAccount.Bic = input.Bic.TrimToNull();
        if (input.BankName is not null)
            bankAccount.BankName = input.BankName.TrimToNull();
        if (input.AccountHolderName is not null)
            bankAccount.AccountHolderName = input.AccountHolderName.TrimToNull();
        if (input.CurrencyCode is not null)
            bankAccount.CurrencyCode = input.CurrencyCode;
        if (input.AccountId is not null)
        {
            bankAccount.AccountId = input.AccountId;
            bankAccount.CounterpartyId = null;
        }
        if (input.CounterpartyId is not null)
        {
            bankAccount.CounterpartyId = input.CounterpartyId;
            bankAccount.AccountId = null;
        }

        EnsureOwnershipXor(bankAccount.AccountId, bankAccount.CounterpartyId);
        EnsureIbanOrAccountNumber(bankAccount.Iban, bankAccount.AccountNumber);

        await EnsureReferencedRowsExistAsync(
            input.CurrencyCode,
            input.AccountId,
            input.CounterpartyId,
            cancellationToken
        );

        await EnsureIbanAvailableAsync(bankAccount.Iban, excludingId: id, cancellationToken);
        await EnsureAccountSlotAvailableAsync(
            bankAccount.AccountId,
            excludingId: id,
            cancellationToken
        );

        bankAccount.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToOutput(bankAccount);
    }

    public async Task DeleteAsync(BankAccountId id, CancellationToken cancellationToken)
    {
        var bankAccount =
            await _dbContext.BankAccounts.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new DomainException(
                DomainExceptionKind.NotFound,
                $"BankAccount {id} not found."
            );

        _dbContext.BankAccounts.Remove(bankAccount);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new DomainException(
                DomainExceptionKind.Conflict,
                $"BankAccount {id} is referenced by other records and cannot be deleted.",
                ex
            );
        }
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

    private static void EnsureOwnershipXor(AccountId? accountId, CounterpartyId? counterpartyId)
    {
        var hasAccount = accountId.HasValue;
        var hasCounterparty = counterpartyId.HasValue;
        if (hasAccount == hasCounterparty)
        {
            throw new DomainException(
                DomainExceptionKind.Invariant,
                "A BankAccount must reference exactly one of AccountId or CounterpartyId."
            );
        }
    }

    private static void EnsureIbanOrAccountNumber(string? iban, string? accountNumber)
    {
        if (iban is null && accountNumber is null)
        {
            throw new DomainException(
                DomainExceptionKind.Invariant,
                "A BankAccount must have at least one of Iban or AccountNumber."
            );
        }
    }

    private async Task EnsureReferencedRowsExistAsync(
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
                throw new DomainException(
                    DomainExceptionKind.NotFound,
                    $"Currency '{code.Value}' is not defined."
                );
            }
        }

        if (accountId is { } aid)
        {
            var exists = await _dbContext.Accounts.AnyAsync(a => a.Id == aid, cancellationToken);
            if (!exists)
            {
                throw new DomainException(
                    DomainExceptionKind.NotFound,
                    $"Account {aid} not found."
                );
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
                throw new DomainException(
                    DomainExceptionKind.NotFound,
                    $"Counterparty {cid} not found."
                );
            }
        }
    }

    private async Task EnsureIbanAvailableAsync(
        string? iban,
        BankAccountId? excludingId,
        CancellationToken cancellationToken
    )
    {
        if (iban is null)
            return;

        var taken = await _dbContext.BankAccounts.AnyAsync(
            b => b.Iban == iban && (excludingId == null || b.Id != excludingId),
            cancellationToken
        );
        if (taken)
        {
            throw new DomainException(
                DomainExceptionKind.Conflict,
                $"A BankAccount with IBAN '{iban}' already exists."
            );
        }
    }

    private async Task EnsureAccountSlotAvailableAsync(
        AccountId? accountId,
        BankAccountId? excludingId,
        CancellationToken cancellationToken
    )
    {
        if (accountId is null)
            return;

        var taken = await _dbContext.BankAccounts.AnyAsync(
            b => b.AccountId == accountId && (excludingId == null || b.Id != excludingId),
            cancellationToken
        );
        if (taken)
        {
            throw new DomainException(
                DomainExceptionKind.Conflict,
                "A BankAccount for that Account already exists."
            );
        }
    }
}
