using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.BankTransactions;

internal sealed class BankTransactionImportService : IBankTransactionImportService
{
    // One retry covers the only realistic race per ADR 0010 (double-clicked submit, two tabs).
    private const int MaxRetries = 1;

    private readonly BalanceDbContext _dbContext;
    private readonly IBankTransactionExtractor _extractor;
    private readonly TimeProvider _timeProvider;

    public BankTransactionImportService(
        BalanceDbContext dbContext,
        IBankTransactionExtractor extractor,
        TimeProvider timeProvider
    )
    {
        _dbContext = dbContext;
        _extractor = extractor;
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

        var extractResult = await _extractor.ExtractAsync(bankAccount, stream, cancellationToken);
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

            DetachAddedBankTransactions();
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

        _dbContext.BankTransactions.AddRange(toInsert);
        var saveResult = await _dbContext.SaveChangesAndCatchAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return new ImportResult(toInsert.Count, skipped);
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
            CreatedAt = now,
            UpdatedAt = now,
        };

    private void DetachAddedBankTransactions()
    {
        var added = _dbContext
            .ChangeTracker.Entries<BankTransaction>()
            .Where(e => e.State == EntityState.Added)
            .ToList();
        foreach (var entry in added)
            entry.State = EntityState.Detached;
    }
}
