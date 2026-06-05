using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// State transitions on the <c>BankTransaction</c>↔<c>JournalEntry</c> link (ADR 0012).
/// Attach validates the 7-condition predicate, sets <c>BankTransaction.JournalEntryId</c>,
/// and flips the matching <c>JournalLine.ReconciliationStatus</c> from <c>Uncleared</c> to
/// <c>Cleared</c> in one transaction. Detach is the inverse.
/// </summary>
public interface IBankTransactionAttachService
{
    Task<Result<JournalEntryDetailOutput>> AttachAsync(
        BankTransactionId id,
        JournalEntryId journalEntryId,
        CancellationToken cancellationToken
    );

    Task<Result<JournalEntryDetailOutput>> DetachAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Server-computed hint shown on Inbox rows: the unique <c>JournalEntry</c> the
    /// 7-condition Attach predicate (ADR 0012) accepts for the given BT, or null if zero
    /// or multiple match. The summary is the minimum the SPA needs to render the badge
    /// without a follow-up detail fetch.
    /// </summary>
    Task<AttachHintOutput?> ComputeHintAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Manual JE-picker fallback (ADR 0013): every <c>JournalEntry</c> that satisfies the
    /// *structural* conditions of the Attach predicate (own-account-only lines, currency
    /// match, an available <c>Uncleared</c> slot on the right <c>Account</c>) but with the
    /// date window as a user-controllable filter. The strict 3-day predicate is reserved
    /// for the auto-hint; this list is what the user widens themselves.
    /// </summary>
    Task<Result<IReadOnlyList<AttachCandidateOutput>>> ListCandidatesAsync(
        BankTransactionId id,
        int dateWindowDays,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Compact summary of the unique JournalEntry the Attach predicate matched for a BT.
/// </summary>
public sealed record AttachHintOutput(
    JournalEntryId Id,
    DateOnly Date,
    string? Description,
    string OtherAccountName
);

/// <summary>
/// One row in the manual JE-picker list (ADR 0013).
/// </summary>
public sealed record AttachCandidateOutput(
    JournalEntryId Id,
    DateOnly Date,
    string? Description,
    string OtherAccountName,
    long Amount
);
