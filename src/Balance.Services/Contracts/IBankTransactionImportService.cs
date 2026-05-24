using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Bank-agnostic orchestrator that turns a statement file uploaded against a chosen
/// <see cref="Balance.Data.Entities.BankAccount"/> into persisted
/// <see cref="Balance.Data.Entities.BankTransaction"/>s. Loads the BankAccount, delegates per-bank
/// parsing and validation to an <see cref="IBankTransactionExtractor"/>, deduplicates extracted
/// rows against existing <c>RowHash</c>es on the BankAccount in a single batched read, bulk-inserts
/// the new rows, and retries once on a unique-index conflict to absorb the concurrent-import race.
/// </summary>
public interface IBankTransactionImportService
{
    Task<Result<ImportResult>> ImportAsync(
        BankAccountId bankAccountId,
        Stream stream,
        CancellationToken cancellationToken
    );
}

public sealed record ImportResult(int Imported, int SkippedAsDuplicate);
