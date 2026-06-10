using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Accounts;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Outlook;

internal sealed class JournalEntryTemplateService : IJournalEntryTemplateService
{
    private readonly BalanceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public JournalEntryTemplateService(BalanceDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<JournalEntryTemplateOutput>> ListAsync(
        CancellationToken cancellationToken
    )
    {
        var templates = await _dbContext
            .JournalEntryTemplates.AsNoTracking()
            .ToListAsync(cancellationToken);
        if (templates.Count == 0)
            return [];

        var accounts = await LoadAccountInfoAsync(cancellationToken);
        var counterparties = await LoadCounterpartyNamesAsync(cancellationToken);
        var today = Today();

        return
        [
            .. templates.OrderBy(t => t.Name).Select(t => Map(t, accounts, counterparties, today)),
        ];
    }

    public async Task<Result<JournalEntryTemplateOutput>> GetAsync(
        JournalEntryTemplateId id,
        CancellationToken cancellationToken
    )
    {
        var template = await _dbContext
            .JournalEntryTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
            return new NotFoundError("JournalEntryTemplate", id.Value.ToString());

        var accounts = await LoadAccountInfoAsync(cancellationToken);
        var counterparties = await LoadCounterpartyNamesAsync(cancellationToken);
        return Map(template, accounts, counterparties, Today());
    }

    public async Task<Result<JournalEntryTemplateOutput>> CreateAsync(
        CreateJournalEntryTemplateInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var accounts = await LoadAccountInfoAsync(cancellationToken);
        var validation = ValidatePinnedAccount(input.AccountId, input.CounterAccountId, accounts);
        if (validation.IsFailure)
            return validation.Error;

        if (
            input.CounterpartyId is { } cpId
            && !await CounterpartyExistsAsync(cpId, cancellationToken)
        )
            return new NotFoundError("Counterparty", cpId.Value.ToString());

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var template = new JournalEntryTemplate
        {
            Id = new JournalEntryTemplateId(Guid.CreateVersion7()),
            Name = input.Name,
            AccountId = input.AccountId,
            CounterAccountId = input.CounterAccountId,
            CounterpartyId = input.CounterpartyId,
            Cadence = input.Cadence,
            AnchorDate = input.AnchorDate,
            EndDate = input.EndDate,
            ExpectedAmount = input.ExpectedAmount,
            MandateId = input.MandateId,
            SepaCreditorId = input.SepaCreditorId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _dbContext.JournalEntryTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var counterparties = await LoadCounterpartyNamesAsync(cancellationToken);
        return Map(template, accounts, counterparties, Today());
    }

    public async Task<Result<JournalEntryTemplateOutput>> UpdateAsync(
        JournalEntryTemplateId id,
        UpdateJournalEntryTemplateInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var template = await _dbContext.JournalEntryTemplates.FirstOrDefaultAsync(
            t => t.Id == id,
            cancellationToken
        );
        if (template is null)
            return new NotFoundError("JournalEntryTemplate", id.Value.ToString());

        var accounts = await LoadAccountInfoAsync(cancellationToken);
        var validation = ValidatePinnedAccount(input.AccountId, input.CounterAccountId, accounts);
        if (validation.IsFailure)
            return validation.Error;

        if (
            input.CounterpartyId is { } cpId
            && !await CounterpartyExistsAsync(cpId, cancellationToken)
        )
            return new NotFoundError("Counterparty", cpId.Value.ToString());

        template.Name = input.Name;
        template.AccountId = input.AccountId;
        template.CounterAccountId = input.CounterAccountId;
        template.CounterpartyId = input.CounterpartyId;
        template.Cadence = input.Cadence;
        template.AnchorDate = input.AnchorDate;
        template.EndDate = input.EndDate;
        template.ExpectedAmount = input.ExpectedAmount;
        template.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var counterparties = await LoadCounterpartyNamesAsync(cancellationToken);
        return Map(template, accounts, counterparties, Today());
    }

    public async Task<Result> DeleteAsync(
        JournalEntryTemplateId id,
        CancellationToken cancellationToken
    )
    {
        var template = await _dbContext.JournalEntryTemplates.FirstOrDefaultAsync(
            t => t.Id == id,
            cancellationToken
        );
        if (template is null)
            return new NotFoundError("JournalEntryTemplate", id.Value.ToString());

        _dbContext.JournalEntryTemplates.Remove(template);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }

    public Task<IReadOnlyList<TemplateCandidateOutput>> DetectCandidatesAsync(
        CancellationToken cancellationToken
    ) => TemplateDetector.DetectAsync(_dbContext, Today(), cancellationToken);

    // ---- mapping & validation -----------------------------------------------------------------

    private static JournalEntryTemplateOutput Map(
        JournalEntryTemplate t,
        IReadOnlyDictionary<AccountId, AccountInfo> accounts,
        IReadOnlyDictionary<CounterpartyId, string> counterparties,
        DateOnly today
    )
    {
        var account = accounts[t.AccountId];
        var delta = AccountSignConvention
            .ToBalance(account.AccountType, t.ExpectedAmount, account.CurrencyCode)
            .Amount;

        return new JournalEntryTemplateOutput(
            t.Id,
            t.Name,
            t.AccountId,
            account.Name,
            t.CounterAccountId,
            t.CounterAccountId is { } caId ? accounts.GetValueOrDefault(caId)?.Name : null,
            t.CounterpartyId,
            t.CounterpartyId is { } cpId ? counterparties.GetValueOrDefault(cpId) : null,
            t.Cadence,
            t.AnchorDate,
            t.EndDate,
            t.ExpectedAmount,
            CadenceMath.MonthlyEquivalent(t.Cadence, delta),
            CadenceMath.NextDueDate(t.Cadence, t.AnchorDate, t.EndDate, today),
            account.CurrencyCode,
            t.MandateId,
            t.SepaCreditorId
        );
    }

    private static Result ValidatePinnedAccount(
        AccountId accountId,
        AccountId? counterAccountId,
        IReadOnlyDictionary<AccountId, AccountInfo> accounts
    )
    {
        if (!accounts.TryGetValue(accountId, out var account))
            return new NotFoundError("Account", accountId.Value.ToString());

        // The pinned account is the bank-side leg: a Liquid, postable balance-sheet account.
        var isLiquidBalanceSheet =
            account.IsPostable
            && account.IsLiquid
            && account.AccountType is AccountType.Asset or AccountType.Liability;
        if (!isLiquidBalanceSheet)
        {
            return new InvariantError(
                "outlook.template.account_not_liquid",
                "A JournalEntryTemplate must be pinned to a liquid, postable Asset or Liability account."
            );
        }

        if (counterAccountId is { } caId && !accounts.ContainsKey(caId))
            return new NotFoundError("Account", caId.Value.ToString());

        return Result.Success;
    }

    private async Task<bool> CounterpartyExistsAsync(
        CounterpartyId id,
        CancellationToken cancellationToken
    ) => await _dbContext.Counterparties.AnyAsync(c => c.Id == id, cancellationToken);

    private async Task<IReadOnlyDictionary<AccountId, AccountInfo>> LoadAccountInfoAsync(
        CancellationToken cancellationToken
    ) =>
        await _dbContext
            .Accounts.AsNoTracking()
            .Select(a => new AccountInfo(
                a.Id,
                a.Name,
                a.AccountType,
                a.CurrencyCode,
                a.IsPostable,
                a.IsLiquid
            ))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

    private async Task<IReadOnlyDictionary<CounterpartyId, string>> LoadCounterpartyNamesAsync(
        CancellationToken cancellationToken
    ) =>
        await _dbContext
            .Counterparties.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

    private DateOnly Today() => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    internal sealed record AccountInfo(
        AccountId Id,
        string Name,
        AccountType AccountType,
        CurrencyCode CurrencyCode,
        bool IsPostable,
        bool IsLiquid
    );
}
