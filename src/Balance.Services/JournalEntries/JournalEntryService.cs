using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.JournalEntries;

internal sealed class JournalEntryService : IJournalEntryService
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private readonly BalanceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public JournalEntryService(BalanceDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<JournalEntry>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken
    )
    {
        var clampedSkip = skip < 0 ? 0 : skip;
        var clampedTake = take <= 0 ? DefaultPageSize : Math.Min(take, MaxPageSize);

        return await _dbContext
            .JournalEntries.AsNoTracking()
            .Include(e => e.Lines)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Skip(clampedSkip)
            .Take(clampedTake)
            .ToListAsync(cancellationToken);
    }

    public Task<JournalEntry?> GetAsync(JournalEntryId id, CancellationToken cancellationToken) =>
        _dbContext
            .JournalEntries.AsNoTracking()
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<JournalEntry> CreateAsync(
        CreateJournalEntryInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Lines);

        var drafts = await BuildDraftsAsync(input.Lines, cancellationToken);
        JournalEntryValidator.Validate(drafts);

        await EnsureOptionalReferencesExistAsync(
            input.BankTransactionId,
            input.CounterpartyId,
            cancellationToken
        );

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entry = new JournalEntry
        {
            Id = new JournalEntryId(Guid.CreateVersion7()),
            Date = input.Date,
            Description = Normalize(input.Description),
            BankTransactionId = input.BankTransactionId,
            CounterpartyId = input.CounterpartyId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var line in input.Lines)
        {
            entry.Lines.Add(
                new JournalLine
                {
                    Id = new JournalLineId(Guid.CreateVersion7()),
                    JournalEntryId = entry.Id,
                    AccountId = line.AccountId,
                    Amount = line.Amount,
                    Description = Normalize(line.Description),
                    CreatedAt = now,
                    UpdatedAt = now,
                }
            );
        }

        _dbContext.JournalEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<JournalEntry> UpdateAsync(
        JournalEntryId id,
        UpdateJournalEntryInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        var entry =
            await _dbContext
                .JournalEntries.Include(e => e.Lines)
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException(
                DomainExceptionKind.NotFound,
                $"JournalEntry {id} not found."
            );

        if (input.Date is not null)
            entry.Date = input.Date.Value;
        if (input.Description is not null)
            entry.Description = Normalize(input.Description);
        if (input.BankTransactionId is not null)
            entry.BankTransactionId = input.BankTransactionId;
        if (input.CounterpartyId is not null)
            entry.CounterpartyId = input.CounterpartyId;

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (input.Lines is not null)
        {
            var drafts = await BuildDraftsAsync(input.Lines, cancellationToken);
            JournalEntryValidator.Validate(drafts);

            _dbContext.JournalLines.RemoveRange(entry.Lines);
            entry.Lines.Clear();

            foreach (var line in input.Lines)
            {
                entry.Lines.Add(
                    new JournalLine
                    {
                        Id = new JournalLineId(Guid.CreateVersion7()),
                        JournalEntryId = entry.Id,
                        AccountId = line.AccountId,
                        Amount = line.Amount,
                        Description = Normalize(line.Description),
                        CreatedAt = now,
                        UpdatedAt = now,
                    }
                );
            }
        }

        await EnsureOptionalReferencesExistAsync(
            input.BankTransactionId,
            input.CounterpartyId,
            cancellationToken
        );

        entry.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task DeleteAsync(JournalEntryId id, CancellationToken cancellationToken)
    {
        var entry =
            await _dbContext.JournalEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException(
                DomainExceptionKind.NotFound,
                $"JournalEntry {id} not found."
            );

        _dbContext.JournalEntries.Remove(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<JournalLineDraft>> BuildDraftsAsync(
        IReadOnlyList<CreateJournalLineInput> lines,
        CancellationToken cancellationToken
    )
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var accountIds = lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.CurrencyCode, cancellationToken);

        var drafts = new List<JournalLineDraft>(lines.Count);
        foreach (var line in lines)
        {
            if (!accounts.TryGetValue(line.AccountId, out var currencyCode))
            {
                throw new DomainException(
                    DomainExceptionKind.NotFound,
                    $"Account {line.AccountId} not found."
                );
            }
            drafts.Add(new JournalLineDraft(line.Amount, currencyCode));
        }
        return drafts;
    }

    private async Task EnsureOptionalReferencesExistAsync(
        BankTransactionId? bankTransactionId,
        CounterpartyId? counterpartyId,
        CancellationToken cancellationToken
    )
    {
        if (bankTransactionId is { } btxId)
        {
            var exists = await _dbContext.BankTransactions.AnyAsync(
                b => b.Id == btxId,
                cancellationToken
            );
            if (!exists)
            {
                throw new DomainException(
                    DomainExceptionKind.NotFound,
                    $"BankTransaction {btxId} not found."
                );
            }
        }

        if (counterpartyId is { } cpId)
        {
            var exists = await _dbContext.Counterparties.AnyAsync(
                c => c.Id == cpId,
                cancellationToken
            );
            if (!exists)
            {
                throw new DomainException(
                    DomainExceptionKind.NotFound,
                    $"Counterparty {cpId} not found."
                );
            }
        }
    }

    private static string? Normalize(string? value)
    {
        if (value is null)
            return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
