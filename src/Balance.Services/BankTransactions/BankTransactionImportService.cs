using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.BankTransactions;

internal sealed class BankTransactionImportService : IBankTransactionImportService
{
    // One retry covers the only realistic race per ADR 0009 (double-clicked submit, two tabs).
    private const int MaxRetries = 1;

    private readonly BalanceDbContext _dbContext;
    private readonly Dictionary<string, IBankTransactionExtractor> _extractorsByKey;
    private readonly TimeProvider _timeProvider;

    public BankTransactionImportService(
        BalanceDbContext dbContext,
        IEnumerable<IBankTransactionExtractor> extractors,
        TimeProvider timeProvider
    )
    {
        _dbContext = dbContext;
        _extractorsByKey = extractors.ToDictionary(e => e.Key, StringComparer.Ordinal);
        _timeProvider = timeProvider;
    }

    public async Task<Result<ImportResult>> ImportAsync(
        BankAccountId bankAccountId,
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(stream);

        var bankAccount = await _dbContext
            .BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bankAccountId, cancellationToken);
        if (bankAccount is null)
            return new NotFoundError("BankAccount", bankAccountId.Value.ToString());

        if (bankAccount.ImporterKey is null)
        {
            return new InvariantError(
                ErrorCodes.ImportBankAccountNotImportable,
                "This BankAccount has no ImporterKey configured; pick an importer on the "
                    + "BankAccount before uploading a statement."
            );
        }

        if (!_extractorsByKey.TryGetValue(bankAccount.ImporterKey, out var extractor))
        {
            return new InvariantError(
                ErrorCodes.ImportBankAccountNotImportable,
                $"No extractor is registered for ImporterKey '{bankAccount.ImporterKey}'."
            );
        }

        if (extractor.SupportedType != bankAccount.Type)
        {
            return new InvariantError(
                ErrorCodes.ImportBankAccountWrongImporter,
                $"Extractor '{extractor.Key}' supports BankAccountType "
                    + $"'{extractor.SupportedType}', but this BankAccount is '{bankAccount.Type}'."
            );
        }

        var extractResult = await extractor.ExtractAsync(bankAccount, stream, cancellationToken);
        if (extractResult.IsFailure)
            return extractResult.Error;

        var extracted = extractResult.Value;
        if (extracted.Count == 0)
            return new ImportResult(0, 0);

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var attemptResult = await PartitionAndSaveAsync(
                bankAccountId,
                extracted,
                cancellationToken
            );
            if (attemptResult.IsSuccess)
                return attemptResult.Value;

            if (attemptResult.Error is not ConflictError { Code: ErrorCodes.UniquenessConflict })
                return attemptResult.Error;

            DetachAddedEntities();
        }

        return new ConflictError(
            ErrorCodes.ImportConcurrentConflict,
            "Concurrent import for this BankAccount lost the race for a duplicate RowHash. "
                + "Retry the upload."
        );
    }

    private async Task<Result<ImportResult>> PartitionAndSaveAsync(
        BankAccountId bankAccountId,
        IReadOnlyList<BankTransaction> extracted,
        CancellationToken cancellationToken
    )
    {
        var existingHashes = await _dbContext
            .BankTransactions.AsNoTracking()
            .Where(b => b.BankAccountId == bankAccountId)
            .Select(b => b.RowHash)
            .ToListAsync(cancellationToken);

        var seen = new HashSet<string>(existingHashes, StringComparer.Ordinal);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var toInsert = new List<BankTransaction>(extracted.Count);
        var skipped = 0;

        foreach (var row in extracted)
        {
            if (!seen.Add(row.RowHash))
            {
                skipped++;
                continue;
            }
            toInsert.Add(Stamp(row, now));
        }

        if (toInsert.Count == 0)
            return new ImportResult(0, skipped);

        await ResolveMetadataKeysAsync(toInsert, now, cancellationToken);

        _dbContext.BankTransactions.AddRange(toInsert);
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return new ImportResult(toInsert.Count, skipped);
    }

    // Extractors stage metadata as values whose `Key` nav carries the key Name but no Id
    // (they don't know which surrogate Id matches that Name). Resolve each Name against
    // the existing BankTransactionMetadataKey rows; create the missing ones in this batch.
    // The unique index on Name protects against concurrent inserts — the outer retry loop
    // covers that race (same path as RowHash collisions).
    private async Task ResolveMetadataKeysAsync(
        IReadOnlyList<BankTransaction> rows,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var keyNames = rows.SelectMany(r => r.Metadata.Select(m => m.Key!.Name))
            .ToHashSet(StringComparer.Ordinal);
        if (keyNames.Count == 0)
            return;

        var existing = await _dbContext
            .BankTransactionMetadataKeys.Where(k => keyNames.Contains(k.Name))
            .ToDictionaryAsync(k => k.Name, StringComparer.Ordinal, cancellationToken);

        foreach (var name in keyNames)
        {
            if (existing.ContainsKey(name))
                continue;

            var key = new BankTransactionMetadataKey
            {
                Id = new BankTransactionMetadataKeyId(Guid.CreateVersion7()),
                Name = name,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _dbContext.BankTransactionMetadataKeys.Add(key);
            existing[name] = key;
        }

        foreach (var row in rows)
        {
            foreach (var value in row.Metadata)
            {
                var resolved = existing[value.Key!.Name];
                value.Key = resolved;
                value.KeyId = resolved.Id;
            }
        }
    }

    // The extractor returns unsaved entities without timestamps (persistence is the orchestrator's
    // concern). BankTransaction's BaseEntity uses init-only Id/CreatedAt, so we reconstruct rather
    // than mutate.
    private static BankTransaction Stamp(BankTransaction row, DateTime now) =>
        new()
        {
            Id = row.Id,
            BankAccountId = row.BankAccountId,
            BookingDate = row.BookingDate,
            Money = row.Money,
            Description = row.Description,
            CounterpartyName = row.CounterpartyName,
            CounterpartyAccountNumber = row.CounterpartyAccountNumber,
            RawSource = row.RawSource,
            RowHash = row.RowHash,
            ValueDate = row.ValueDate,
            Reference = row.Reference,
            MandateId = row.MandateId,
            SepaCreditorId = row.SepaCreditorId,
            ForeignAmount = row.ForeignAmount,
            ForeignCurrencyCode = row.ForeignCurrencyCode,
            ExchangeRate = row.ExchangeRate,
            ImporterKey = row.ImporterKey,
            CreatedAt = now,
            UpdatedAt = now,
            Metadata = row.Metadata,
        };

    private void DetachAddedEntities()
    {
        var added = _dbContext
            .ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added)
            .ToList();
        foreach (var entry in added)
            entry.State = EntityState.Detached;
    }
}
