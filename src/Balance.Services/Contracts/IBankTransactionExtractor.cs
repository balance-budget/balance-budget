using Balance.Data.Entities;

namespace Balance.Services.Contracts;

/// <summary>
/// Per-bank extractor that turns a bank's statement file into unsaved <see cref="BankTransaction"/>s
/// for a given <see cref="BankAccount"/>. Per-bank validation (Account column matches the chosen
/// BankAccount, currency matches, ownership) lives here. The bank-agnostic
/// <c>BankTransactionImportService</c> orchestrates dedup and persistence around it.
/// </summary>
public interface IBankTransactionExtractor
{
    public Task<Result<IReadOnlyList<BankTransaction>>> ExtractAsync(
        BankAccount bankAccount,
        Stream stream,
        CancellationToken cancellationToken
    );
}
