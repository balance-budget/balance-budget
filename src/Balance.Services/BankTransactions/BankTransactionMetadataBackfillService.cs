using Balance.Configuration.Helpers;
using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Balance.Services.BankTransactions;

/// <summary>
/// One-shot idempotent re-extraction backfill (issue #89). Iterates every
/// <see cref="BankTransaction"/> with a non-null <c>ImporterKey</c>, dispatches each row to its
/// registered <see cref="IBankTransactionReExtractor"/>, fills any null promoted columns, and
/// replaces the metadata set wholesale.
///
/// Throwaway — invoked once from <c>Program.cs</c> after <c>MigrateDatabaseAsync</c>; a follow-up
/// PR removes this file alongside <see cref="IBankTransactionReExtractor"/> once it has run
/// against the production database.
/// </summary>
public static class BankTransactionMetadataBackfillService
{
    public static async Task RunAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        await using var scope = serviceProvider.CreateAsyncScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        // Same gate MigrateDatabaseAsync uses — the OpenAPI generator runs the host at
        // design time and there's no migrated database to scan.
        if (environment.IsDesignTime())
            return;

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<BalanceDbContext>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var extractors = scope
            .ServiceProvider.GetServices<IBankTransactionReExtractor>()
            .ToDictionary(e => e.ImporterKey, StringComparer.Ordinal);

        logger.BankTransactionReExtractionBackfillStarted();

        var ids = await dbContext
            .BankTransactions.AsNoTracking()
            .OrderBy(b => b.Id)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        var visited = 0;
        var updated = 0;
        var skippedNoImporterKey = 0;
        var skippedNoExtractor = 0;
        var skippedExtractorError = 0;
        var now = DateTime.UtcNow;

        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            visited++;

            var row = await dbContext
                .BankTransactions.Include(b => b.Metadata)
                    .ThenInclude(m => m.Key)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
            if (row is null)
                continue;

            if (string.IsNullOrEmpty(row.ImporterKey))
            {
                skippedNoImporterKey++;
                continue;
            }

            if (!extractors.TryGetValue(row.ImporterKey, out var extractor))
            {
                logger.BankTransactionReExtractionMissingExtractor(row.Id.Value, row.ImporterKey);
                skippedNoExtractor++;
                continue;
            }

            var result = await extractor.ReExtractAsync(row.RawSource, cancellationToken);
            if (result.IsFailure)
            {
                var errorMessage = result.Error switch
                {
                    InvariantError inv => inv.Message,
                    ConflictError conf => conf.Message,
                    _ => result.Error.Code,
                };
                logger.BankTransactionReExtractionExtractorError(
                    row.Id.Value,
                    row.ImporterKey,
                    result.Error.Code,
                    errorMessage
                );
                skippedExtractorError++;
                continue;
            }

            await ApplyAsync(dbContext, row, result.Value, now, cancellationToken);
            updated++;
        }

        logger.BankTransactionReExtractionBackfillFinished(
            visited,
            updated,
            skippedNoImporterKey,
            skippedNoExtractor,
            skippedExtractorError
        );
    }

    // Fills any null promoted columns and replaces the metadata set wholesale. Rerunning is a
    // no-op: already-populated promoted columns stay put, and re-replacing the metadata set
    // with the same content converges to the same result.
    private static async Task ApplyAsync(
        BalanceDbContext dbContext,
        BankTransaction row,
        BankTransactionReExtraction extracted,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        // Promoted columns are init-only, so we can't mutate the loaded row directly.
        // Detach + re-attach with the merged column values; the EF Update marks every column as
        // modified. RowHash / RawSource / identifying columns are preserved untouched.
        var entry = dbContext.Entry(row);
        var existingMetadata = row.Metadata.ToList();
        entry.State = EntityState.Detached;
        foreach (var value in existingMetadata)
            dbContext.Entry(value).State = EntityState.Detached;

        var merged = new BankTransaction
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
            ValueDate = row.ValueDate ?? extracted.ValueDate,
            Reference = row.Reference ?? extracted.Reference,
            MandateId = row.MandateId ?? extracted.MandateId,
            SepaCreditorId = row.SepaCreditorId ?? extracted.SepaCreditorId,
            ForeignAmount = row.ForeignAmount ?? extracted.ForeignAmount,
            ForeignCurrencyCode = row.ForeignCurrencyCode ?? extracted.ForeignCurrencyCode,
            ExchangeRate = row.ExchangeRate ?? extracted.ExchangeRate,
            ImporterKey = row.ImporterKey,
            DismissedAt = row.DismissedAt,
            DismissedReason = row.DismissedReason,
            CreatedAt = row.CreatedAt,
            UpdatedAt = now,
        };

        dbContext.BankTransactions.Update(merged);

        // Wipe existing metadata for this row and insert the freshly-extracted set. Cascade-
        // delete on BankTransaction → BankTransactionMetadataValue isn't engaged here because
        // we keep the parent; explicit RemoveRange.
        dbContext.BankTransactionMetadataValues.RemoveRange(existingMetadata);

        await ResolveAndAttachMetadataKeysAsync(
            dbContext,
            row.Id,
            extracted.Metadata,
            now,
            cancellationToken
        );

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Mirror of BankTransactionImportService.ResolveMetadataKeysAsync, scoped to a single row:
    // map each staged value's Key.Name to an existing or newly-inserted
    // BankTransactionMetadataKey, set KeyId + BankTransactionId, and add to the context.
    private static async Task ResolveAndAttachMetadataKeysAsync(
        BalanceDbContext dbContext,
        BankTransactionId bankTransactionId,
        IReadOnlyList<BankTransactionMetadataValue> values,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        if (values.Count == 0)
            return;

        var keyNames = values.Select(v => v.Key!.Name).ToHashSet(StringComparer.Ordinal);
        var existing = await dbContext
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
            dbContext.BankTransactionMetadataKeys.Add(key);
            existing[name] = key;
        }

        foreach (var value in values)
        {
            var resolved = existing[value.Key!.Name];
            var attached = new BankTransactionMetadataValue
            {
                BankTransactionId = bankTransactionId,
                KeyId = resolved.Id,
                StringValue = value.StringValue,
                IntegerValue = value.IntegerValue,
            };
            dbContext.BankTransactionMetadataValues.Add(attached);
        }
    }
}
