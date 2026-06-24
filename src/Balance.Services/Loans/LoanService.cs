using Balance.Data;
using Balance.Data.Configurations;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Accounts;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Loans;

internal sealed class LoanService : ILoanService
{
    private readonly BalanceDbContext _dbContext;
    private readonly AccountFactory _accountFactory;
    private readonly LoanReader _reader;
    private readonly TimeProvider _timeProvider;

    public LoanService(
        BalanceDbContext dbContext,
        AccountFactory accountFactory,
        LoanReader reader,
        TimeProvider timeProvider
    )
    {
        _dbContext = dbContext;
        _accountFactory = accountFactory;
        _reader = reader;
        _timeProvider = timeProvider;
    }

    public Task<IReadOnlyList<LoanOutput>> ListAsync(CancellationToken cancellationToken) =>
        _reader.ListAsync(cancellationToken);

    public async Task<Result<LoanDetailOutput>> GetAsync(
        LoanId id,
        CancellationToken cancellationToken
    )
    {
        var detail = await _reader.GetDetailAsync(id, cancellationToken);
        return detail is null ? new NotFoundError("Loan", id.Value.ToString()) : detail;
    }

    public async Task<Result<UpdateLoanInput>> GetSnapshotAsync(
        LoanId id,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await _dbContext
            .Loans.AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new UpdateLoanInput
            {
                Name = l.Name,
                LenderCounterpartyId = l.LenderCounterpartyId,
                InterestExpenseAccountId = l.InterestExpenseAccountId,
                ConstructionDepositAccountId = l.ConstructionDepositAccountId,
                ConstructionDepositInterestIncomeAccountId =
                    l.ConstructionDepositInterestIncomeAccountId,
                ConstructionDepositAnnualRatePercent = l.ConstructionDepositAnnualRatePercent,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return snapshot is null ? new NotFoundError("Loan", id.Value.ToString()) : snapshot;
    }

    public async Task<Result<LoanDetailOutput>> CreateAsync(
        CreateLoanInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Parts);

        if (input.Parts.Count == 0)
        {
            return new InvariantError(
                ErrorCodes.LoanPartAccountSelection,
                "A Loan requires at least one Loan Part."
            );
        }

        var referencesCheck = await EnsureReferencesAsync(
            input.LenderCounterpartyId,
            input.InterestExpenseAccountId,
            cancellationToken
        );
        if (referencesCheck.IsFailure)
            return referencesCheck.Error;

        var currencyCheck = await _dbContext
            .Currencies.Where(c => c.Code == input.CurrencyCode)
            .EnsureExistsAsync("Currency", input.CurrencyCode.Value, cancellationToken);
        if (currencyCheck.IsFailure)
            return currencyCheck.Error;

        var depositCheck = await EnsureDepositReferencesAsync(
            input.ConstructionDepositAccountId,
            input.ConstructionDepositInterestIncomeAccountId,
            input.ConstructionDepositAnnualRatePercent,
            input.CurrencyCode,
            cancellationToken
        );
        if (depositCheck.IsFailure)
            return depositCheck.Error;

        // All accounts created or adopted in one transaction: a part-level failure rolls back
        // the parent account and earlier siblings.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var parentResult = await CreateAccountAsync(
            input.ParentAccountName,
            input.ParentAccountCode,
            input.CurrencyCode,
            postable: false,
            parentId: null,
            now,
            cancellationToken
        );
        if (parentResult.IsFailure)
            return parentResult.Error;
        var parent = parentResult.Value!;

        var loan = new Loan
        {
            Id = new LoanId(Guid.CreateVersion7()),
            Name = input.Name.Trim(),
            LenderCounterpartyId = input.LenderCounterpartyId,
            InterestExpenseAccountId = input.InterestExpenseAccountId,
            ParentAccountId = parent.Id,
            ConstructionDepositAccountId = input.ConstructionDepositAccountId,
            ConstructionDepositInterestIncomeAccountId =
                input.ConstructionDepositInterestIncomeAccountId,
            ConstructionDepositAnnualRatePercent = input.ConstructionDepositAnnualRatePercent,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _dbContext.Loans.Add(loan);

        foreach (var partInput in input.Parts)
        {
            var partResult = await AddPartCoreAsync(
                loan,
                parent,
                partInput,
                now,
                cancellationToken
            );
            if (partResult.IsFailure)
                return partResult.Error;
        }

        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(loan.Id, cancellationToken);
    }

    public async Task<Result<LoanDetailOutput>> UpdateAsync(
        LoanId id,
        UpdateLoanInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null)
            return new NotFoundError("Loan", id.Value.ToString());

        var referencesCheck = await EnsureReferencesAsync(
            input.LenderCounterpartyId,
            input.InterestExpenseAccountId,
            cancellationToken
        );
        if (referencesCheck.IsFailure)
            return referencesCheck.Error;

        var trimmedName = input.Name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
        {
            return new InvariantError(ErrorCodes.RequestInvalid, "Loan name cannot be empty.");
        }

        var loanCurrency = await _dbContext
            .Accounts.Where(a => a.Id == loan.ParentAccountId)
            .Select(a => a.CurrencyCode)
            .FirstAsync(cancellationToken);
        var depositCheck = await EnsureDepositReferencesAsync(
            input.ConstructionDepositAccountId,
            input.ConstructionDepositInterestIncomeAccountId,
            input.ConstructionDepositAnnualRatePercent,
            loanCurrency,
            cancellationToken
        );
        if (depositCheck.IsFailure)
            return depositCheck.Error;

        loan.Name = trimmedName;
        loan.LenderCounterpartyId = input.LenderCounterpartyId;
        loan.InterestExpenseAccountId = input.InterestExpenseAccountId;
        loan.ConstructionDepositAccountId = input.ConstructionDepositAccountId;
        loan.ConstructionDepositInterestIncomeAccountId =
            input.ConstructionDepositInterestIncomeAccountId;
        loan.ConstructionDepositAnnualRatePercent = input.ConstructionDepositAnnualRatePercent;
        loan.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return await GetAsync(loan.Id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(LoanId id, CancellationToken cancellationToken)
    {
        // Rate periods and parts cascade; JournalLine attributions SET NULL; the accounts and
        // their history stay (ADR-0025: the ledger is the source of truth, the loan is a layer).
        return await _dbContext
            .Loans.Where(l => l.Id == id)
            .DeleteSingleAndCatchAsync("Loan", id.Value.ToString(), cancellationToken);
    }

    public async Task<Result<LoanDetailOutput>> AddPartAsync(
        LoanId id,
        CreateLoanPartInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null)
            return new NotFoundError("Loan", id.Value.ToString());

        var parent = await _dbContext.Accounts.FirstAsync(
            a => a.Id == loan.ParentAccountId,
            cancellationToken
        );

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var partResult = await AddPartCoreAsync(loan, parent, input, now, cancellationToken);
        if (partResult.IsFailure)
            return partResult.Error;

        loan.UpdatedAt = now;
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(loan.Id, cancellationToken);
    }

    public async Task<Result<LoanDetailOutput>> AddRatePeriodAsync(
        LoanId id,
        LoanPartId partId,
        CreateLoanRatePeriodInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var part = await _dbContext.LoanParts.FirstOrDefaultAsync(
            p => p.Id == partId && p.LoanId == id,
            cancellationToken
        );
        if (part is null)
            return new NotFoundError("LoanPart", partId.Value.ToString());

        var conflictCheck = await _dbContext
            .LoanPartRatePeriods.Where(r =>
                r.LoanPartId == partId && r.EffectiveDate == input.EffectiveDate
            )
            .EnsureNoneAsync(
                ErrorCodes.LoanRatePeriodConflict,
                $"A rate period effective {input.EffectiveDate:yyyy-MM-dd} already exists for this part.",
                cancellationToken
            );
        if (conflictCheck.IsFailure)
            return conflictCheck.Error;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        _dbContext.LoanPartRatePeriods.Add(
            new LoanPartRatePeriod
            {
                Id = new LoanPartRatePeriodId(Guid.CreateVersion7()),
                LoanPartId = partId,
                EffectiveDate = input.EffectiveDate,
                AnnualRatePercent = input.AnnualRatePercent,
                FixedUntil = input.FixedUntil,
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
        part.UpdatedAt = now;

        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<LoanDetailOutput>> UpdatePartAsync(
        LoanId id,
        LoanPartId partId,
        UpdateLoanPartInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var part = await _dbContext.LoanParts.FirstOrDefaultAsync(
            p => p.Id == partId && p.LoanId == id,
            cancellationToken
        );
        if (part is null)
            return new NotFoundError("LoanPart", partId.Value.ToString());

        var label = input.Label?.Trim() ?? string.Empty;
        if (label.Length == 0)
            return new InvariantError(
                ErrorCodes.RequestInvalid,
                "Loan Part label cannot be empty."
            );
        if (input.EndDate <= input.StartDate)
            return new InvariantError(
                ErrorCodes.RequestInvalid,
                "EndDate must be after StartDate."
            );

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        part.Label = label;
        part.RepaymentType = input.RepaymentType;
        part.StartDate = input.StartDate;
        part.EndDate = input.EndDate;
        part.UpdatedAt = now;

        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<LoanDetailOutput>> DeletePartAsync(
        LoanId id,
        LoanPartId partId,
        CancellationToken cancellationToken
    )
    {
        var partExists = await _dbContext.LoanParts.AnyAsync(
            p => p.Id == partId && p.LoanId == id,
            cancellationToken
        );
        if (!partExists)
            return new NotFoundError("LoanPart", partId.Value.ToString());

        var partCount = await _dbContext.LoanParts.CountAsync(
            p => p.LoanId == id,
            cancellationToken
        );
        if (partCount <= 1)
        {
            return new InvariantError(
                ErrorCodes.LoanLastPart,
                "A Loan must keep at least one part. Delete the whole Loan instead."
            );
        }

        // Rate periods cascade; JournalLine attributions on the part's account SET NULL; the
        // account and its posted history stay (ADR-0025).
        var deleteResult = await _dbContext
            .LoanParts.Where(p => p.Id == partId && p.LoanId == id)
            .DeleteSingleAndCatchAsync("LoanPart", partId.Value.ToString(), cancellationToken);
        if (deleteResult.IsFailure)
            return deleteResult.Error;

        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<LoanDetailOutput>> UpdateRatePeriodAsync(
        LoanId id,
        LoanPartId partId,
        LoanPartRatePeriodId ratePeriodId,
        CreateLoanRatePeriodInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var rate = await _dbContext
            .LoanPartRatePeriods.Where(r =>
                r.Id == ratePeriodId
                && r.LoanPartId == partId
                && _dbContext.LoanParts.Any(p => p.Id == partId && p.LoanId == id)
            )
            .FirstOrDefaultAsync(cancellationToken);
        if (rate is null)
            return new NotFoundError("LoanPartRatePeriod", ratePeriodId.Value.ToString());

        var conflictCheck = await _dbContext
            .LoanPartRatePeriods.Where(r =>
                r.LoanPartId == partId
                && r.EffectiveDate == input.EffectiveDate
                && r.Id != ratePeriodId
            )
            .EnsureNoneAsync(
                ErrorCodes.LoanRatePeriodConflict,
                $"A rate period effective {input.EffectiveDate:yyyy-MM-dd} already exists for this part.",
                cancellationToken
            );
        if (conflictCheck.IsFailure)
            return conflictCheck.Error;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        rate.EffectiveDate = input.EffectiveDate;
        rate.AnnualRatePercent = input.AnnualRatePercent;
        rate.FixedUntil = input.FixedUntil;
        rate.UpdatedAt = now;

        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<LoanDetailOutput>> DeleteRatePeriodAsync(
        LoanId id,
        LoanPartId partId,
        LoanPartRatePeriodId ratePeriodId,
        CancellationToken cancellationToken
    )
    {
        var rateExists = await _dbContext.LoanPartRatePeriods.AnyAsync(
            r =>
                r.Id == ratePeriodId
                && r.LoanPartId == partId
                && _dbContext.LoanParts.Any(p => p.Id == partId && p.LoanId == id),
            cancellationToken
        );
        if (!rateExists)
            return new NotFoundError("LoanPartRatePeriod", ratePeriodId.Value.ToString());

        var rateCount = await _dbContext.LoanPartRatePeriods.CountAsync(
            r => r.LoanPartId == partId,
            cancellationToken
        );
        if (rateCount <= 1)
        {
            return new InvariantError(
                ErrorCodes.LoanRatePeriodLastRemaining,
                "A Loan Part must keep at least one rate period."
            );
        }

        var deleteResult = await _dbContext
            .LoanPartRatePeriods.Where(r => r.Id == ratePeriodId && r.LoanPartId == partId)
            .DeleteSingleAndCatchAsync(
                "LoanPartRatePeriod",
                ratePeriodId.Value.ToString(),
                cancellationToken
            );
        if (deleteResult.IsFailure)
            return deleteResult.Error;

        return await GetAsync(id, cancellationToken);
    }

    private async Task<Result> AddPartCoreAsync(
        Loan loan,
        Account parent,
        CreateLoanPartInput input,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input.RatePeriods);

        if (input.AdoptAccountId.HasValue == (input.NewAccount is not null))
        {
            return new InvariantError(
                ErrorCodes.LoanPartAccountSelection,
                "Provide exactly one of AdoptAccountId or NewAccount per Loan Part."
            );
        }

        if (input.RatePeriods.Count == 0)
        {
            return new InvariantError(
                ErrorCodes.LoanRatePeriodConflict,
                "A Loan Part requires at least one rate period."
            );
        }

        if (
            input.RatePeriods.Select(r => r.EffectiveDate).Distinct().Count()
            != input.RatePeriods.Count
        )
        {
            return new InvariantError(
                ErrorCodes.LoanRatePeriodConflict,
                "Rate periods must have distinct effective dates."
            );
        }

        AccountId accountId;
        if (input.AdoptAccountId is { } adoptId)
        {
            var adoptionResult = await AdoptAccountAsync(adoptId, parent, now, cancellationToken);
            if (adoptionResult.IsFailure)
                return adoptionResult.Error;
            accountId = adoptId;
        }
        else
        {
            var newAccount = input.NewAccount!;
            var accountResult = await CreateAccountAsync(
                newAccount.Name,
                newAccount.Code,
                parent.CurrencyCode,
                postable: true,
                parent.Id,
                now,
                cancellationToken
            );
            if (accountResult.IsFailure)
                return accountResult.Error;
            accountId = accountResult.Value!.Id;

            if (newAccount.OpeningBalance != 0)
                AddOpeningBalanceEntry(accountId, newAccount, now);
        }

        var part = new LoanPart
        {
            Id = new LoanPartId(Guid.CreateVersion7()),
            LoanId = loan.Id,
            Label = input.Label.Trim(),
            RepaymentType = input.RepaymentType,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            AccountId = accountId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        foreach (var rate in input.RatePeriods)
        {
            part.RatePeriods.Add(
                new LoanPartRatePeriod
                {
                    Id = new LoanPartRatePeriodId(Guid.CreateVersion7()),
                    LoanPartId = part.Id,
                    EffectiveDate = rate.EffectiveDate,
                    AnnualRatePercent = rate.AnnualRatePercent,
                    FixedUntil = rate.FixedUntil,
                    CreatedAt = now,
                    UpdatedAt = now,
                }
            );
        }

        _dbContext.LoanParts.Add(part);
        return Result.Success;
    }

    // Adoption constraints (ADR-0025): Liability, postable, currency match, childless, not
    // already loan-managed. Re-parents the leaf under the loan with history intact and defaults
    // it to Illiquid — a mortgage should keep out of liquid net worth without manual fiddling.
    private async Task<Result> AdoptAccountAsync(
        AccountId adoptId,
        Account parent,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var account = await _dbContext.Accounts.FirstOrDefaultAsync(
            a => a.Id == adoptId,
            cancellationToken
        );
        if (account is null)
            return new NotFoundError("Account", adoptId.Value.ToString());

        if (account.AccountType != AccountType.Liability || !account.IsPostable)
        {
            return new InvariantError(
                ErrorCodes.LoanPartAccountInvalid,
                "Only a postable Liability account can be adopted as a Loan Part."
            );
        }

        if (account.CurrencyCode != parent.CurrencyCode)
        {
            return new InvariantError(
                ErrorCodes.LoanPartAccountInvalid,
                $"Account {adoptId.Value} is denominated in {account.CurrencyCode.Value}; "
                    + $"the loan uses {parent.CurrencyCode.Value}."
            );
        }

        var hasChildren = await _dbContext.Accounts.AnyAsync(
            a => a.ParentAccountId == adoptId,
            cancellationToken
        );
        if (hasChildren)
        {
            return new InvariantError(
                ErrorCodes.LoanPartAccountInvalid,
                "An account with children cannot be adopted as a Loan Part."
            );
        }

        var managed = await LoanManagedAccounts.FindLoanManagedAsync(
            _dbContext,
            [adoptId],
            cancellationToken
        );
        if (managed.Count > 0)
            return LoanManagedAccounts.Refusal(adoptId);

        // The pending graph isn't visible to the queries above: refuse adopting the same
        // account twice within one create call.
        var pendingTwice = _dbContext
            .ChangeTracker.Entries<LoanPart>()
            .Any(e => e.State == EntityState.Added && e.Entity.AccountId == adoptId);
        if (pendingTwice)
            return LoanManagedAccounts.Refusal(adoptId);

        account.ParentAccountId = parent.Id;
        account.IsLiquid = false;
        // A loan part is illiquid debt held for the long haul (ADR-0030).
        account.Horizon = Horizon.LongTerm;
        account.UpdatedAt = now;
        return Result.Success;
    }

    private Task<Result<Account>> CreateAccountAsync(
        string name,
        string code,
        CurrencyCode currencyCode,
        bool postable,
        AccountId? parentId,
        DateTime now,
        CancellationToken cancellationToken
    ) =>
        _accountFactory.StageAsync(
            new NewAccount(
                name,
                code,
                AccountType.Liability,
                currencyCode,
                postable,
                IsLiquid: false,
                Horizon.ShortTerm,
                parentId,
                IconName: null,
                now
            ),
            cancellationToken
        );

    // The existing Opening balance convention: pair the account against the seeded Opening
    // Balances equity account, both legs Reconciled, no bank import. A liability's opening
    // principal is a credit, hence the negated amount on the part side.
    private void AddOpeningBalanceEntry(
        AccountId accountId,
        NewLoanPartAccountInput input,
        DateTime now
    )
    {
        var entry = new JournalEntry
        {
            Id = new JournalEntryId(Guid.CreateVersion7()),
            Date = input.OpeningDate,
            Description = "Opening balance",
            CounterpartyId = null,
            CreatedAt = now,
            UpdatedAt = now,
        };
        entry.Lines.Add(OpeningLine(entry.Id, accountId, -input.OpeningBalance, now));
        entry.Lines.Add(
            OpeningLine(entry.Id, AccountSeed.OpeningBalancesId, input.OpeningBalance, now)
        );
        _dbContext.JournalEntries.Add(entry);
    }

    private static JournalLine OpeningLine(
        JournalEntryId entryId,
        AccountId accountId,
        long amount,
        DateTime now
    ) =>
        new()
        {
            Id = new JournalLineId(Guid.CreateVersion7()),
            JournalEntryId = entryId,
            AccountId = accountId,
            Amount = amount,
            ReconciliationStatus = ReconciliationStatus.Reconciled,
            CreatedAt = now,
            UpdatedAt = now,
        };

    private async Task<Result> EnsureReferencesAsync(
        CounterpartyId lenderCounterpartyId,
        AccountId interestExpenseAccountId,
        CancellationToken cancellationToken
    )
    {
        var lenderCheck = await _dbContext
            .Counterparties.Where(c => c.Id == lenderCounterpartyId)
            .EnsureExistsAsync(
                "Counterparty",
                lenderCounterpartyId.Value.ToString(),
                cancellationToken
            );
        if (lenderCheck.IsFailure)
            return lenderCheck.Error;

        var interestAccount = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.Id == interestExpenseAccountId)
            .Select(a => new { a.AccountType, a.IsPostable })
            .FirstOrDefaultAsync(cancellationToken);
        if (interestAccount is null)
            return new NotFoundError("Account", interestExpenseAccountId.Value.ToString());

        if (interestAccount.AccountType != AccountType.Expense || !interestAccount.IsPostable)
        {
            return new InvariantError(
                ErrorCodes.LoanInterestAccountInvalid,
                "The interest account must be a postable Expense account."
            );
        }

        return Result.Success;
    }

    // The Construction deposit triple (ADR-0026): all three set or none. When set, the deposit is
    // a plain postable Asset account (not loan-managed) in the loan's currency, the income account
    // a postable Income account, and the rate within [0, 100].
    private async Task<Result> EnsureDepositReferencesAsync(
        AccountId? depositAccountId,
        AccountId? incomeAccountId,
        decimal? annualRatePercent,
        CurrencyCode loanCurrency,
        CancellationToken cancellationToken
    )
    {
        var anySet =
            depositAccountId is not null
            || incomeAccountId is not null
            || annualRatePercent is not null;
        var allSet =
            depositAccountId is not null
            && incomeAccountId is not null
            && annualRatePercent is not null;
        if (!anySet)
            return Result.Success;
        if (!allSet)
        {
            return new InvariantError(
                ErrorCodes.LoanDepositReferencesIncomplete,
                "A Construction deposit needs an asset account, an income account, and a rate — set all three or none."
            );
        }

        if (annualRatePercent is < 0m or > 100m)
        {
            return new InvariantError(
                ErrorCodes.RequestInvalid,
                "The Construction deposit rate must be between 0 and 100."
            );
        }

        var depositAccount = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.Id == depositAccountId)
            .Select(a => new
            {
                a.AccountType,
                a.IsPostable,
                a.CurrencyCode,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (depositAccount is null)
            return new NotFoundError("Account", depositAccountId!.Value.Value.ToString());
        if (
            depositAccount.AccountType != AccountType.Asset
            || !depositAccount.IsPostable
            || depositAccount.CurrencyCode != loanCurrency
        )
        {
            return new InvariantError(
                ErrorCodes.LoanDepositAccountInvalid,
                $"The Construction deposit must be a postable Asset account in {loanCurrency.Value}."
            );
        }

        var managed = await LoanManagedAccounts.FindLoanManagedAsync(
            _dbContext,
            [depositAccountId!.Value],
            cancellationToken
        );
        if (managed.Count > 0)
        {
            return new InvariantError(
                ErrorCodes.LoanDepositAccountInvalid,
                "The Construction deposit account must not be loan-managed."
            );
        }

        var incomeAccount = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.Id == incomeAccountId)
            .Select(a => new { a.AccountType, a.IsPostable })
            .FirstOrDefaultAsync(cancellationToken);
        if (incomeAccount is null)
            return new NotFoundError("Account", incomeAccountId!.Value.Value.ToString());
        if (incomeAccount.AccountType != AccountType.Income || !incomeAccount.IsPostable)
        {
            return new InvariantError(
                ErrorCodes.LoanDepositIncomeAccountInvalid,
                "The Construction deposit interest account must be a postable Income account."
            );
        }

        return Result.Success;
    }
}
