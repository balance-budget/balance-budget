using System.Globalization;
using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Data.Helpers;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
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

    public async Task<IReadOnlyList<JournalEntryRowOutput>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken
    )
    {
        var rows = await _dbContext
            .JournalEntries.AsNoTracking()
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(e => new EntryProjectionRow(
                e.Id,
                e.Date,
                e.Description,
                e.BankTransactionId,
                e.CounterpartyId,
                e.CounterpartyId == null
                    ? null
                    : _dbContext
                        .Counterparties.Where(c => c.Id == e.CounterpartyId)
                        .Select(c => c.Name)
                        .FirstOrDefault(),
                e.CreatedAt,
                e.UpdatedAt,
                e.Lines.Select(l => new EntryProjectionLine(
                        l.Id,
                        l.AccountId,
                        _dbContext
                            .Accounts.Where(a => a.Id == l.AccountId)
                            .Select(a => a.Name)
                            .First(),
                        _dbContext
                            .Accounts.Where(a => a.Id == l.AccountId)
                            .Select(a => a.AccountType)
                            .First(),
                        _dbContext
                            .Accounts.Where(a => a.Id == l.AccountId)
                            .Select(a => a.CurrencyCode)
                            .First(),
                        l.Amount,
                        l.ReconciliationStatus,
                        l.Description
                    ))
                    .ToList()
            ))
            .ToListAsync(cancellationToken);

        var output = new List<JournalEntryRowOutput>(rows.Count);
        foreach (var row in rows)
        {
            output.Add(BuildRowOutput(row));
        }

        return output;
    }

    private static (
        Money NetWorthChange,
        Money GrossMagnitude,
        JournalEntryProjectionResult Result
    ) Project(EntryProjectionRow row)
    {
        // All lines in a balanced entry share a currency (enforced by JournalEntryValidator).
        // For the empty-or-degenerate edge case, fall back to a sentinel so the projection
        // can still wrap zero amounts as Money.
        var currencyCode =
            row.Lines.Count > 0 ? row.Lines[0].CurrencyCode : new CurrencyCode("XXX");

        var projectionInputs = row
            .Lines.Select(l => new JournalLineProjectionInput(
                l.AccountId,
                l.AccountName,
                l.AccountType,
                l.Amount
            ))
            .ToList();

        var projection = JournalEntryProjection.Compute(projectionInputs);
        return (
            new Money(projection.NetWorthChange, currencyCode),
            new Money(projection.GrossMagnitude, currencyCode),
            projection
        );
    }

    private static JournalEntryRowOutput BuildRowOutput(EntryProjectionRow row)
    {
        var (netWorthChange, grossMagnitude, projection) = Project(row);
        return new JournalEntryRowOutput(
            row.Id,
            row.Date,
            row.Description,
            row.BankTransactionId,
            row.CounterpartyId,
            row.CounterpartyName,
            row.Lines.Count,
            projection.IsTransfer,
            netWorthChange,
            grossMagnitude,
            projection.IsSimplifiable,
            projection.FromLegs,
            projection.ToLegs,
            row.CreatedAt,
            row.UpdatedAt
        );
    }

    private static JournalEntryOutput BuildDetailOutput(EntryProjectionRow row)
    {
        var (netWorthChange, grossMagnitude, projection) = Project(row);
        var lines = row
            .Lines.Select(l => new JournalLineOutput(
                l.Id,
                l.AccountId,
                l.AccountName,
                l.Amount,
                l.ReconciliationStatus,
                l.Description
            ))
            .ToList();
        return new JournalEntryOutput(
            row.Id,
            row.Date,
            row.Description,
            row.BankTransactionId,
            row.CounterpartyId,
            row.CounterpartyName,
            row.Lines.Count,
            projection.IsTransfer,
            netWorthChange,
            grossMagnitude,
            projection.IsSimplifiable,
            projection.FromLegs,
            projection.ToLegs,
            lines,
            row.CreatedAt,
            row.UpdatedAt
        );
    }

    private sealed record EntryProjectionRow(
        JournalEntryId Id,
        DateOnly Date,
        string? Description,
        BankTransactionId? BankTransactionId,
        CounterpartyId? CounterpartyId,
        string? CounterpartyName,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        IReadOnlyList<EntryProjectionLine> Lines
    );

    private sealed record EntryProjectionLine(
        JournalLineId Id,
        AccountId AccountId,
        string AccountName,
        AccountType AccountType,
        CurrencyCode CurrencyCode,
        long Amount,
        ReconciliationStatus ReconciliationStatus,
        string? Description
    );

    public async Task<Result<JournalEntryOutput>> GetAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    )
    {
        var row = await QueryDetailAsync(id, cancellationToken);
        if (row is null)
            return new NotFoundError("JournalEntry", id.Value.ToString());
        return BuildDetailOutput(row);
    }

    private Task<EntryProjectionRow?> QueryDetailAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    )
    {
        return _dbContext
            .JournalEntries.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EntryProjectionRow(
                e.Id,
                e.Date,
                e.Description,
                e.BankTransactionId,
                e.CounterpartyId,
                e.CounterpartyId == null
                    ? null
                    : _dbContext
                        .Counterparties.Where(c => c.Id == e.CounterpartyId)
                        .Select(c => c.Name)
                        .FirstOrDefault(),
                e.CreatedAt,
                e.UpdatedAt,
                e.Lines.Select(l => new EntryProjectionLine(
                        l.Id,
                        l.AccountId,
                        _dbContext
                            .Accounts.Where(a => a.Id == l.AccountId)
                            .Select(a => a.Name)
                            .First(),
                        _dbContext
                            .Accounts.Where(a => a.Id == l.AccountId)
                            .Select(a => a.AccountType)
                            .First(),
                        _dbContext
                            .Accounts.Where(a => a.Id == l.AccountId)
                            .Select(a => a.CurrencyCode)
                            .First(),
                        l.Amount,
                        l.ReconciliationStatus,
                        l.Description
                    ))
                    .ToList()
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<UpdateJournalEntryInput>> GetSnapshotAsync(
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
            return new NotFoundError("JournalEntry", id.Value.ToString());
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

    public async Task<Result<JournalEntryOutput>> CreateAsync(
        CreateJournalEntryInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Lines);

        var draftsResult = await BuildDraftsAsync(input.Lines, cancellationToken);
        if (draftsResult.IsFailure)
        {
            return draftsResult.Error;
        }

        var balanceCheck = JournalEntryValidator.Validate(draftsResult.Value);
        if (balanceCheck.IsFailure)
            return balanceCheck.Error;

        var referencesCheck = await EnsureOptionalReferencesExistAsync(
            input.BankTransactionId,
            input.CounterpartyId,
            cancellationToken
        );
        if (referencesCheck.IsFailure)
            return referencesCheck.Error;

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
                    ReconciliationStatus = line.ReconciliationStatus,
                    Description = line.Description.TrimToNull(),
                    CreatedAt = now,
                    UpdatedAt = now,
                }
            );
        }

        _dbContext.JournalEntries.Add(entry);
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return await LoadDetailOrThrowAsync(entry.Id, cancellationToken);
    }

    public async Task<Result<JournalEntryOutput>> UpdateAsync(
        JournalEntryId id,
        UpdateJournalEntryInput input,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Lines);

        var entry = await _dbContext
            .JournalEntries.Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entry is null)
        {
            return new NotFoundError("JournalEntry", id.Value.ToString());
        }

        // Parse the dictionary keys back to typed IDs and enforce key-set equality. Adds /
        // removals / reorders are explicitly rejected — corrections to the *set* of lines
        // go through delete-and-recreate (or a future reversing-entry flow), preserving
        // each line's ReconciliationStatus across edits.
        var existingIds = entry.Lines.Select(l => l.Id).ToHashSet();
        var parsedKeys = new Dictionary<JournalLineId, UpdateJournalLineInput>(existingIds.Count);
        foreach (var (key, lineInput) in input.Lines)
        {
            if (!Guid.TryParseExact(key, "D", out var guid))
            {
                return new InvariantError(
                    ErrorCodes.JournalLineKeyInvalid,
                    $"JournalLine key '{key}' is not a valid identifier."
                );
            }

            parsedKeys[new JournalLineId(guid)] = lineInput;
        }

        if (!parsedKeys.Keys.ToHashSet().SetEquals(existingIds))
        {
            return new InvariantError(
                ErrorCodes.JournalLineSetMismatch,
                "JournalLines cannot be added or removed via PATCH; use a reversing entry."
            );
        }

        if (input.CounterpartyId != entry.CounterpartyId)
        {
            var referencesCheck = await EnsureOptionalReferencesExistAsync(
                bankTransactionId: null,
                input.CounterpartyId,
                cancellationToken
            );
            if (referencesCheck.IsFailure)
                return referencesCheck.Error;
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
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return await LoadDetailOrThrowAsync(entry.Id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(JournalEntryId id, CancellationToken cancellationToken)
    {
        var result = await _dbContext
            .JournalEntries.Where(c => c.Id == id)
            .ExecuteDeleteAndCatchAsync(cancellationToken);

        if (result.IsFailure)
            return result.Error;

        if (result.Value == 0)
            return new NotFoundError("JournalEntry", id.Value.ToString());

        return Result.Success;
    }

    private async Task<JournalEntryOutput> LoadDetailOrThrowAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    )
    {
        var row = await QueryDetailAsync(id, cancellationToken);
        if (row is null)
        {
            throw new InvalidOperationException(
                $"JournalEntry {id.Value} disappeared between save and re-query."
            );
        }

        return BuildDetailOutput(row);
    }

    private async Task<Result<IReadOnlyList<JournalLineDraft>>> BuildDraftsAsync(
        IReadOnlyList<CreateJournalLineInput> lines,
        CancellationToken cancellationToken
    )
    {
        if (lines.Count == 0)
        {
            return new Result<IReadOnlyList<JournalLineDraft>>(Array.Empty<JournalLineDraft>());
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
                return new NotFoundError("Account", line.AccountId.Value.ToString());
            }

            drafts.Add(new JournalLineDraft(line.Amount, currencyCode));
        }

        return new Result<IReadOnlyList<JournalLineDraft>>(drafts);
    }

    private async Task<Result> EnsureOptionalReferencesExistAsync(
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
                return new NotFoundError("BankTransaction", btxId.Value.ToString());
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
                return new NotFoundError("Counterparty", cpId.Value.ToString());
            }
        }

        return Result.Success;
    }
}
