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
    /// produces. Bank-prefixed and version-free — the logical importer identity, not a statement
    /// layout version, e.g. <c>Ing.CurrentAccount</c> or <c>Ing.CreditCard</c> (ADR 0034).
    /// </summary>
    string Key { get; }

    /// <summary>
    /// The proper-noun name of the bank this extractor reads, e.g. <c>"ING"</c>. A presentation
    /// hint only: the frontend composes the human importer label from this plus the
    /// (translated) <see cref="SupportedType"/> word, so no user-facing copy lives in backend
    /// code (ADR 0022/0034).
    /// </summary>
    string BankName { get; }

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

    /// <summary>
    /// Probe a dropped file (no chosen BankAccount) for the account it belongs to, for the
    /// drop-and-detect flow (ADR 0034). Returns the recognized <see cref="ImportIdentity"/> or
    /// <c>null</c> when this extractor does not recognize the file. Implementations take the
    /// fastest reliable anchor source — a filename the bank embeds the identifier in, otherwise
    /// the file content — and must leave <see cref="ImportFile.Content"/> seekable for callers
    /// (the eventual import re-reads and re-validates it).
    /// </summary>
    Task<ImportIdentity?> TryIdentifyAsync(ImportFile file, CancellationToken cancellationToken);
}
