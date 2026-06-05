using Balance.Data.Entities;
using Balance.Data.Entities.Enums;

namespace Balance.Services.Contracts;

/// <summary>
/// Per-bank extractor that turns a bank's statement file into unsaved <see cref="BankTransaction"/>s
/// for a given <see cref="BankAccount"/>. Per-bank validation (Account column matches the chosen
/// BankAccount, currency matches, ownership) lives here. The bank-agnostic
/// <c>BankTransactionImportService</c> orchestrates dedup and persistence around it, dispatching
/// by <see cref="BankAccount.ImporterKey"/> across the registered extractors (ADR 0009).
/// </summary>
public interface IBankTransactionExtractor
{
    /// <summary>
    /// Stable identifier for this extractor; matches <see cref="BankAccount.ImporterKey"/> and
    /// the <c>ImporterKey</c> stamped on every <see cref="BankTransaction"/> the extractor
    /// produces. Bank-prefixed and version-suffixed, e.g. <c>Ing.CurrentAccount.V1</c>.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// The <see cref="BankAccountType"/> this extractor accepts. The dispatcher refuses any
    /// pairing where the BankAccount's <c>Type</c> disagrees with this value.
    /// </summary>
    BankAccountType SupportedType { get; }

    Task<Result<IReadOnlyList<BankTransaction>>> ExtractAsync(
        BankAccount bankAccount,
        Stream stream,
        CancellationToken cancellationToken
    );
}
