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

    public async Task<PagedOutput<JournalEntryOutput>> ListAsync(
        int skip,
        int take,
        string? search,
        CancellationToken cancellationToken
    )
    {
        IQueryable<JournalEntry> filtered = _dbContext.JournalEntries;
        var needle = search?.Trim();
        if (!string.IsNullOrEmpty(needle))
        {
            filtered = filtered.Where(e =>
                e.Description != null && EF.Functions.Like(e.Description, $"%{needle}%")
            );
        }

        var totalCount = await filtered.CountAsync(cancellationToken);
        var page = filtered
            .AsNoTracking()
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take);
        var items = await ProjectListOutput(_dbContext, page).ToListAsync(cancellationToken);
        return new PagedOutput<JournalEntryOutput>(items, totalCount);
    }

    public async Task<Result<JournalEntryDetailOutput>> GetAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    )
    {
        var query = _dbContext.JournalEntries.AsNoTracking().Where(e => e.Id == id);
        var output = await ProjectDetailOutput(_dbContext, query)
            .FirstOrDefaultAsync(cancellationToken);
        if (output is null)
            return new NotFoundError("JournalEntry", id.Value.ToString());
        return output;
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

    public async Task<Result<JournalEntryDetailOutput>> CreateAsync(
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

        var referencesCheck = await EnsureCounterpartyExistsAsync(
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

    public async Task<Result<JournalEntryDetailOutput>> UpdateAsync(
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
            var referencesCheck = await EnsureCounterpartyExistsAsync(
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

    public async Task<Result<JournalEntryDetailOutput>> ReplaceAsync(
        JournalEntryId id,
        ReplaceJournalEntryInput input,
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

        var existingLines = entry.Lines.ToDictionary(l => l.Id);

        // Editability gate (ADR 0016): every line whose current ReconciliationStatus != Uncleared
        // must appear in the body with unchanged AccountId and Amount; existing non-Uncleared lines
        // missing from the body are not deletable; new lines (no Id) default to Uncleared; body-
        // supplied ReconciliationStatus is validated to match current (the PUT does not mutate it).
        var referencedIds = new HashSet<JournalLineId>();
        foreach (var lineInput in input.Lines)
        {
            if (lineInput.Id is not { } lineId)
                continue;

            if (!existingLines.TryGetValue(lineId, out var existing))
            {
                return new InvariantError(
                    ErrorCodes.JournalLineUnknown,
                    $"JournalLine {lineId.Value} does not belong to JournalEntry {id.Value}."
                );
            }

            if (!referencedIds.Add(lineId))
            {
                return new InvariantError(
                    ErrorCodes.JournalLineSetMismatch,
                    $"JournalLine {lineId.Value} appears more than once in the body."
                );
            }

            if (existing.ReconciliationStatus != ReconciliationStatus.Uncleared)
            {
                if (
                    existing.AccountId != lineInput.AccountId
                    || existing.Amount != lineInput.Amount
                )
                {
                    return new InvariantError(
                        ErrorCodes.JournalLineFrozen,
                        $"JournalLine {lineId.Value} is {existing.ReconciliationStatus}; "
                            + "AccountId and Amount are frozen — only Description is editable."
                    );
                }
            }

            if (
                lineInput.ReconciliationStatus is { } status
                && status != existing.ReconciliationStatus
            )
            {
                return new InvariantError(
                    ErrorCodes.JournalLineStatusMutation,
                    $"JournalLine {lineId.Value} ReconciliationStatus is immutable on this endpoint."
                );
            }
        }

        foreach (var existing in entry.Lines)
        {
            if (
                existing.ReconciliationStatus != ReconciliationStatus.Uncleared
                && !referencedIds.Contains(existing.Id)
            )
            {
                return new InvariantError(
                    ErrorCodes.JournalLineSetMismatch,
                    $"JournalLine {existing.Id.Value} is {existing.ReconciliationStatus} and "
                        + "cannot be removed via PUT; only Uncleared lines may be deleted."
                );
            }
        }

        // Account / currency / sum-to-zero check on the *final* line set (the body shape).
        IReadOnlyList<CreateJournalLineInput> draftInputs =
        [
            .. input.Lines.Select(l => new CreateJournalLineInput(
                l.AccountId,
                l.Amount,
                l.Description
            )),
        ];
        var draftsResult = await BuildDraftsAsync(draftInputs, cancellationToken);
        if (draftsResult.IsFailure)
            return draftsResult.Error;

        var balanceCheck = JournalEntryValidator.Validate(draftsResult.Value);
        if (balanceCheck.IsFailure)
            return balanceCheck.Error;

        if (input.CounterpartyId is not null && input.CounterpartyId != entry.CounterpartyId)
        {
            var referencesCheck = await EnsureCounterpartyExistsAsync(
                input.CounterpartyId,
                cancellationToken
            );
            if (referencesCheck.IsFailure)
                return referencesCheck.Error;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        entry.Date = input.Date;
        entry.Description = input.Description.TrimToNull();
        entry.CounterpartyId = input.CounterpartyId;
        entry.UpdatedAt = now;

        // Reshape the lines: update existing referenced ones, insert new ones (no Id),
        // and delete existing Uncleared ones that the body omitted.
        foreach (var lineInput in input.Lines)
        {
            if (lineInput.Id is { } lineId)
            {
                var existing = existingLines[lineId];
                existing.AccountId = lineInput.AccountId;
                existing.Amount = lineInput.Amount;
                existing.Description = lineInput.Description.TrimToNull();
                existing.UpdatedAt = now;
                continue;
            }

            entry.Lines.Add(
                new JournalLine
                {
                    Id = new JournalLineId(Guid.CreateVersion7()),
                    JournalEntryId = entry.Id,
                    AccountId = lineInput.AccountId,
                    Amount = lineInput.Amount,
                    ReconciliationStatus = ReconciliationStatus.Uncleared,
                    Description = lineInput.Description.TrimToNull(),
                    CreatedAt = now,
                    UpdatedAt = now,
                }
            );
        }

        // Delete existing Uncleared lines that the body omitted. Newly-added lines have ids that
        // aren't in `existingLines`, so they're naturally excluded; non-Uncleared lines were
        // already guarded above (their omission would have returned an error).
        foreach (var existing in existingLines.Values)
        {
            if (referencedIds.Contains(existing.Id))
                continue;
            entry.Lines.Remove(existing);
            _dbContext.JournalLines.Remove(existing);
        }

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

    private async Task<JournalEntryDetailOutput> LoadDetailOrThrowAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    )
    {
        var query = _dbContext.JournalEntries.AsNoTracking().Where(e => e.Id == id);
        var output = await ProjectDetailOutput(_dbContext, query)
            .FirstOrDefaultAsync(cancellationToken);
        if (output is null)
        {
            throw new InvalidOperationException(
                $"JournalEntry {id.Value} disappeared between save and re-query."
            );
        }

        return output;
    }

    /// <summary>
    /// EF projection from a JournalEntry queryable to the list wire shape, with name
    /// joins for Counterparty and Account. The caller applies ordering/paging before
    /// projecting — the nested Lines collection blocks server-side ordering on the projected
    /// shape.
    /// </summary>
    private static IQueryable<JournalEntryOutput> ProjectListOutput(
        BalanceDbContext db,
        IQueryable<JournalEntry> source
    )
    {
        return source.Select(e => new JournalEntryOutput(
            e.Id,
            e.Date,
            e.Description,
            e.CounterpartyId,
            e.CounterpartyId == null
                ? null
                : db
                    .Counterparties.Where(c => c.Id == e.CounterpartyId)
                    .Select(c => c.Name)
                    .FirstOrDefault(),
            e.Lines.Select(l => new JournalLineOutput(
                    l.Id,
                    l.AccountId,
                    db.Accounts.Where(a => a.Id == l.AccountId).Select(a => a.Name).First(),
                    l.Amount,
                    l.ReconciliationStatus,
                    l.Description
                ))
                .ToList(),
            e.CreatedAt,
            e.UpdatedAt,
            db.BankTransactions.Any(b => b.JournalEntryId == e.Id)
        ));
    }

    /// <summary>
    /// EF projection for the detail wire shape. Adds the list of bank-transactions
    /// pointing at this entry via <c>BankTransaction.JournalEntryId</c> (each with its
    /// metadata bag). The list is 0 or 1 elements today (per-BT FK cardinality); the
    /// list shape is forward-compatible with ADR 0013's self-transfer Attach. The
    /// metadata join is wasted work for the list endpoint, so this variant is
    /// reserved for Get / Create / Update.
    /// </summary>
    private static IQueryable<JournalEntryDetailOutput> ProjectDetailOutput(
        BalanceDbContext db,
        IQueryable<JournalEntry> source
    )
    {
        return source.Select(e => new JournalEntryDetailOutput(
            e.Id,
            e.Date,
            e.Description,
            e.CounterpartyId,
            e.CounterpartyId == null
                ? null
                : db
                    .Counterparties.Where(c => c.Id == e.CounterpartyId)
                    .Select(c => c.Name)
                    .FirstOrDefault(),
            e.Lines.Select(l => new JournalLineOutput(
                    l.Id,
                    l.AccountId,
                    db.Accounts.Where(a => a.Id == l.AccountId).Select(a => a.Name).First(),
                    l.Amount,
                    l.ReconciliationStatus,
                    l.Description
                ))
                .ToList(),
            e.CreatedAt,
            e.UpdatedAt,
            db.BankTransactions.Where(b => b.JournalEntryId == e.Id)
                .OrderBy(b => b.BookingDate)
                .ThenBy(b => b.CreatedAt)
                .Select(b => new BankTransactionDetailOutput(
                    b.Id,
                    b.BankAccountId,
                    b.BookingDate,
                    b.Money,
                    b.Description,
                    b.CounterpartyName,
                    b.CounterpartyAccountNumber,
                    b.ValueDate,
                    b.Reference,
                    b.MandateId,
                    b.SepaCreditorId,
                    b.ForeignAmount,
                    b.ForeignCurrencyCode,
                    b.ExchangeRate,
                    b.ImporterKey,
                    b.JournalEntryId,
                    b.DismissedAt,
                    b.DismissedReason,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.Metadata.OrderBy(m => m.Key!.Name)
                        .Select(m => new BankTransactionMetadataEntryOutput(
                            m.Key!.Name,
                            m.StringValue,
                            m.IntegerValue
                        ))
                        .ToList()
                ))
                .ToList()
        ));
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

    private async Task<Result> EnsureCounterpartyExistsAsync(
        CounterpartyId? counterpartyId,
        CancellationToken cancellationToken
    )
    {
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
