using System.Globalization;
using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Data.Helpers;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.JournalEntries;

internal sealed class JournalEntryService : IJournalEntryService
{
    private readonly BalanceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public JournalEntryService(BalanceDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<JournalEntryOutput>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken
    )
    {
        return await _dbContext
            .JournalEntries.OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(e => new JournalEntryOutput(
                e.Id,
                e.Date,
                e.Description,
                e.BankTransactionId,
                e.CounterpartyId,
                e.Lines.Select(l => new JournalLineOutput(
                        l.Id,
                        l.AccountId,
                        l.Amount,
                        l.ReconciliationStatus,
                        l.Description
                    ))
                    .ToList(),
                e.CreatedAt,
                e.UpdatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public Task<JournalEntryOutput?> GetAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    ) =>
        _dbContext
            .JournalEntries.Where(e => e.Id == id)
            .Select(e => new JournalEntryOutput(
                e.Id,
                e.Date,
                e.Description,
                e.BankTransactionId,
                e.CounterpartyId,
                e.Lines.Select(l => new JournalLineOutput(
                        l.Id,
                        l.AccountId,
                        l.Amount,
                        l.ReconciliationStatus,
                        l.Description
                    ))
                    .ToList(),
                e.CreatedAt,
                e.UpdatedAt
            ))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<UpdateJournalEntryInput?> GetSnapshotAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await _dbContext
            .JournalEntries.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.Date,
                e.Description,
                e.CounterpartyId,
                Lines = e.Lines.Select(l => new { l.Id, l.Description }).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        return new UpdateJournalEntryInput
        {
            Date = snapshot.Date,
            Description = snapshot.Description,
            CounterpartyId = snapshot.CounterpartyId,
            Lines = snapshot.Lines.ToDictionary(
                l => l.Id.Value.ToString("D", CultureInfo.InvariantCulture),
                l => new UpdateJournalLineInput { Description = l.Description }
            ),
        };
    }

    public async Task<JournalEntryOutput> CreateAsync(
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
            Description = input.Description.TrimToNull(),
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
                    Description = line.Description.TrimToNull(),
                    CreatedAt = now,
                    UpdatedAt = now,
                }
            );
        }

        _dbContext.JournalEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToOutput(entry);
    }

    public async Task<JournalEntryOutput> UpdateAsync(
        JournalEntryId id,
        UpdateJournalEntryInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Lines);

        var entry =
            await _dbContext
                .JournalEntries.Include(e => e.Lines)
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException(
                DomainExceptionKind.NotFound,
                $"JournalEntry {id} not found."
            );

        // Parse the dictionary keys back to typed IDs and enforce key-set equality. Adds /
        // removals / reorders are explicitly rejected — corrections to the *set* of lines
        // go through delete-and-recreate (or a future reversing-entry flow), preserving
        // each line's ReconciliationStatus across edits.
        var existingIds = entry.Lines.Select(l => l.Id).ToHashSet();
        var parsedKeys = new Dictionary<JournalLineId, UpdateJournalLineInput>(existingIds.Count);
        foreach (var (key, lineInput) in input.Lines)
        {
            if (
                !Guid.TryParseExact(key, "D", out var guid)
                && !Guid.TryParse(key, CultureInfo.InvariantCulture, out guid)
            )
            {
                throw new DomainException(
                    DomainExceptionKind.Invariant,
                    $"JournalLine key '{key}' is not a valid identifier."
                );
            }
            parsedKeys[new JournalLineId(guid)] = lineInput;
        }

        if (!parsedKeys.Keys.ToHashSet().SetEquals(existingIds))
        {
            throw new DomainException(
                DomainExceptionKind.Invariant,
                "JournalLines cannot be added or removed via PATCH; use a reversing entry."
            );
        }

        if (input.CounterpartyId != entry.CounterpartyId)
        {
            await EnsureOptionalReferencesExistAsync(
                bankTransactionId: null,
                input.CounterpartyId,
                cancellationToken
            );
        }

        entry.Date = input.Date;
        entry.Description = input.Description.TrimToNull();
        entry.CounterpartyId = input.CounterpartyId;

        foreach (var line in entry.Lines)
        {
            var lineInput = parsedKeys[line.Id];
            line.Description = lineInput.Description.TrimToNull();
        }

        entry.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToOutput(entry);
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

    private static JournalEntryOutput ToOutput(JournalEntry entry) =>
        new(
            entry.Id,
            entry.Date,
            entry.Description,
            entry.BankTransactionId,
            entry.CounterpartyId,
            [
                .. entry.Lines.Select(l => new JournalLineOutput(
                    l.Id,
                    l.AccountId,
                    l.Amount,
                    l.ReconciliationStatus,
                    l.Description
                )),
            ],
            entry.CreatedAt,
            entry.UpdatedAt
        );

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
}
