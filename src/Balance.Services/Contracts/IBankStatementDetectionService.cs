using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// The drop-and-detect import orchestrator (ADR 0034). For each dropped file it asks every
/// <see cref="IBankTransactionExtractor"/> to identify the file, resolves the
/// <see cref="ImportIdentity.AccountAnchor"/> to an owned <c>BankAccount</c>, and — only when the
/// match is unambiguous — imports it through the bank-agnostic
/// <see cref="IBankTransactionImportService"/> (which re-validates the file content). Every other
/// case (no match, ambiguous, not importable, unrecognized, failed) is reported for the user to
/// resolve manually; detection never silently imports or drops a file.
/// </summary>
public interface IBankStatementDetectionService
{
    Task<IReadOnlyList<DetectedImportOutcome>> DetectAndImportAsync(
        IReadOnlyList<ImportFile> files,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// The result of detecting (and, when confident, importing) a single dropped file.
/// </summary>
public sealed record DetectedImportOutcome(
    string FileName,
    ImportFileStatus Status,
    BankAccountId? BankAccountId,
    string? AccountAnchor,
    int Imported,
    int SkippedAsDuplicate,
    string? Detail
);

/// <summary>
/// The outcome of detecting a dropped statement file. Only <see cref="Imported"/> is automatic;
/// the rest surface for manual resolution.
/// </summary>
public enum ImportFileStatus
{
    /// Detected an unambiguous owned account and imported the file.
    Imported,

    /// No registered importer recognized the file.
    Unrecognized,

    /// Recognized, but the account anchor matched no owned BankAccount.
    NoMatchingAccount,

    /// The anchor matched more than one owned BankAccount.
    AmbiguousMatch,

    /// The matched BankAccount has no importer configured.
    NotImportable,

    /// Recognized and matched, but the import itself failed (e.g. content re-validation).
    Failed,
}
