using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
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
    private readonly Dictionary<string, IBankTransactionExtractor> _extractorsByKey;
    private readonly TimeProvider _timeProvider;

    public BankAccountService(
        BalanceDbContext dbContext,
        ICurrencyService currencyService,
        IEnumerable<IBankTransactionExtractor> extractors,
        TimeProvider timeProvider
    )
    {
        _dbContext = dbContext;
        _currencyService = currencyService;
        _extractorsByKey = extractors.ToDictionary(e => e.Key, StringComparer.Ordinal);
        _timeProvider = timeProvider;
    }

    public async Task<PagedOutput<BankAccountOutput>> ListAsync(CancellationToken cancellationToken)
    {
        var items = await _dbContext
            .BankAccounts.OrderBy(b => b.CreatedAt)
            .Select(b => new BankAccountOutput(
                b.Id,
                b.Type,
                b.Iban,
                b.AccountNumber,
                b.CardIdentifier,
                b.Bic,
                b.BankName,
                b.AccountHolderName,
                b.CurrencyCode,
                b.ImporterKey,
                b.AccountId,
                b.CounterpartyId,
                b.CreatedAt,
                b.UpdatedAt
            ))
            .ToListAsync(cancellationToken);
        return new PagedOutput<BankAccountOutput>(items, items.Count);
    }

    public async Task<Result<BankAccountOutput>> GetAsync(
        BankAccountId id,
        CancellationToken cancellationToken
    )
    {
        var output = await _dbContext
            .BankAccounts.Where(b => b.Id == id)
            .Select(b => new BankAccountOutput(
                b.Id,
                b.Type,
                b.Iban,
                b.AccountNumber,
                b.CardIdentifier,
                b.Bic,
                b.BankName,
                b.AccountHolderName,
                b.CurrencyCode,
                b.ImporterKey,
                b.AccountId,
                b.CounterpartyId,
                b.CreatedAt,
                b.UpdatedAt
            ))
            .FirstOrDefaultAsync(cancellationToken);
        return output is null ? new NotFoundError("BankAccount", id.Value.ToString()) : output;
    }

    public async Task<Result<UpdateBankAccountInput>> GetSnapshotAsync(
        BankAccountId id,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await _dbContext
            .BankAccounts.AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new UpdateBankAccountInput
            {
                Type = b.Type,
                Iban = b.Iban,
                AccountNumber = b.AccountNumber,
                CardIdentifier = b.CardIdentifier,
                Bic = b.Bic,
                BankName = b.BankName,
                AccountHolderName = b.AccountHolderName,
                CurrencyCode = b.CurrencyCode,
                ImporterKey = b.ImporterKey,
                AccountId = b.AccountId,
                CounterpartyId = b.CounterpartyId,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return snapshot is null ? new NotFoundError("BankAccount", id.Value.ToString()) : snapshot;
    }

    public async Task<Result<BankAccountOutput>> CreateAsync(
        CreateBankAccountInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var iban = input.Iban.TrimToNull();
        var accountNumber = input.AccountNumber.TrimToNull();
        var cardIdentifier = input.CardIdentifier.TrimToNull();
        var bic = input.Bic.TrimToNull();
        var bankName = input.BankName.TrimToNull();
        var accountHolderName = input.AccountHolderName.TrimToNull();
        var importerKey = input.ImporterKey.TrimToNull();

        var ownershipCheck = EnsureOwnershipXor(input.AccountId, input.CounterpartyId);
        if (ownershipCheck.IsFailure)
            return ownershipCheck.Error;

        var typeCheck = EnsureValidForType(
            input.Type,
            iban,
            accountNumber,
            cardIdentifier,
            input.AccountId
        );
        if (typeCheck.IsFailure)
            return typeCheck.Error;

        var currencyCheck = EnsureCurrencyWhenOwned(input.AccountId, input.CurrencyCode);
        if (currencyCheck.IsFailure)
            return currencyCheck.Error;

        var importerCheck = EnsureImporterMatchesType(importerKey, input.Type);
        if (importerCheck.IsFailure)
            return importerCheck.Error;

        var referencesCheck = await EnsureReferencedRowsExistAsync(
            input.CurrencyCode,
            input.AccountId,
            input.CounterpartyId,
            cancellationToken
        );
        if (referencesCheck.IsFailure)
            return referencesCheck.Error;

        var ibanCheck = await EnsureIbanAvailableAsync(iban, excludingId: null, cancellationToken);
        if (ibanCheck.IsFailure)
            return ibanCheck.Error;

        var slotCheck = await EnsureAccountSlotAvailableAsync(
            input.AccountId,
            excludingId: null,
            cancellationToken
        );
        if (slotCheck.IsFailure)
            return slotCheck.Error;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var bankAccount = new BankAccount
        {
            Id = new BankAccountId(Guid.CreateVersion7()),
            Type = input.Type,
            Iban = iban,
            AccountNumber = accountNumber,
            CardIdentifier = cardIdentifier,
            Bic = bic,
            BankName = bankName,
            AccountHolderName = accountHolderName,
            CurrencyCode = input.CurrencyCode,
            ImporterKey = importerKey,
            AccountId = input.AccountId,
            CounterpartyId = input.CounterpartyId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _dbContext.BankAccounts.Add(bankAccount);
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

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
        var cardIdentifier = input.CardIdentifier.TrimToNull();
        var bic = input.Bic.TrimToNull();
        var bankName = input.BankName.TrimToNull();
        var accountHolderName = input.AccountHolderName.TrimToNull();
        var importerKey = input.ImporterKey.TrimToNull();

        var ownershipCheck = EnsureOwnershipXor(input.AccountId, input.CounterpartyId);
        if (ownershipCheck.IsFailure)
            return ownershipCheck.Error;

        var typeCheck = EnsureValidForType(
            input.Type,
            iban,
            accountNumber,
            cardIdentifier,
            input.AccountId
        );
        if (typeCheck.IsFailure)
            return typeCheck.Error;

        var currencyCheck = EnsureCurrencyWhenOwned(input.AccountId, input.CurrencyCode);
        if (currencyCheck.IsFailure)
            return currencyCheck.Error;

        var importerCheck = EnsureImporterMatchesType(importerKey, input.Type);
        if (importerCheck.IsFailure)
            return importerCheck.Error;

        var referencesCheck = await EnsureReferencedRowsExistAsync(
            input.CurrencyCode,
            input.AccountId,
            input.CounterpartyId,
            cancellationToken
        );
        if (referencesCheck.IsFailure)
            return referencesCheck.Error;

        var ibanCheck = await EnsureIbanAvailableAsync(iban, excludingId: id, cancellationToken);
        if (ibanCheck.IsFailure)
            return ibanCheck.Error;

        var slotCheck = await EnsureAccountSlotAvailableAsync(
            input.AccountId,
            excludingId: id,
            cancellationToken
        );
        if (slotCheck.IsFailure)
            return slotCheck.Error;

        bankAccount.Type = input.Type;
        bankAccount.Iban = iban;
        bankAccount.AccountNumber = accountNumber;
        bankAccount.CardIdentifier = cardIdentifier;
        bankAccount.Bic = bic;
        bankAccount.BankName = bankName;
        bankAccount.AccountHolderName = accountHolderName;
        bankAccount.CurrencyCode = input.CurrencyCode;
        bankAccount.ImporterKey = importerKey;
        bankAccount.AccountId = input.AccountId;
        bankAccount.CounterpartyId = input.CounterpartyId;
        bankAccount.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

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
            bankAccount.Type,
            bankAccount.Iban,
            bankAccount.AccountNumber,
            bankAccount.CardIdentifier,
            bankAccount.Bic,
            bankAccount.BankName,
            bankAccount.AccountHolderName,
            bankAccount.CurrencyCode,
            bankAccount.ImporterKey,
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

    private static Result EnsureValidForType(
        BankAccountType type,
        string? iban,
        string? accountNumber,
        string? cardIdentifier,
        AccountId? accountId
    )
    {
        switch (type)
        {
            case BankAccountType.Current:
                if (iban is null)
                {
                    return new InvariantError(
                        ErrorCodes.BankAccountIdentifierMissing,
                        "A Current BankAccount requires an Iban."
                    );
                }
                break;
            case BankAccountType.Savings:
                if (iban is null && accountNumber is null)
                {
                    return new InvariantError(
                        ErrorCodes.BankAccountIdentifierMissing,
                        "A Savings BankAccount requires an Iban or AccountNumber."
                    );
                }
                break;
            case BankAccountType.Card:
                if (cardIdentifier is null)
                {
                    return new InvariantError(
                        ErrorCodes.BankAccountIdentifierMissing,
                        "A Card BankAccount requires a CardIdentifier."
                    );
                }
                if (!accountId.HasValue)
                {
                    return new InvariantError(
                        ErrorCodes.BankAccountCardOwnedOnly,
                        "A Card BankAccount must be owned by an Account (counterparty Cards are not supported)."
                    );
                }
                break;
            default:
                return new InvariantError(
                    ErrorCodes.RequestInvalid,
                    $"Unknown BankAccountType '{type}'."
                );
        }

        return Result.Success;
    }

    private static Result EnsureCurrencyWhenOwned(AccountId? accountId, CurrencyCode? currencyCode)
    {
        if (accountId.HasValue && currencyCode is null)
        {
            return new InvariantError(
                ErrorCodes.BankAccountCurrencyRequiredWhenOwned,
                "A BankAccount that represents one of your own Accounts must have a CurrencyCode."
            );
        }

        return Result.Success;
    }

    private Result EnsureImporterMatchesType(string? importerKey, BankAccountType type)
    {
        if (importerKey is null)
            return Result.Success;

        if (!_extractorsByKey.TryGetValue(importerKey, out var extractor))
        {
            return new InvariantError(
                ErrorCodes.BankAccountImporterUnknown,
                $"No extractor is registered for ImporterKey '{importerKey}'."
            );
        }

        if (extractor.SupportedType != type)
        {
            return new InvariantError(
                ErrorCodes.BankAccountImporterTypeMismatch,
                $"Importer '{importerKey}' supports BankAccountType '{extractor.SupportedType}', "
                    + $"which does not match this BankAccount's Type '{type}'."
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
            var currency = await _currencyService.GetAsync(code, cancellationToken);
            if (currency.IsFailure)
            {
                return currency.Error;
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
