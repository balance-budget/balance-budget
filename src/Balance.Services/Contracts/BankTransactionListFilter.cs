namespace Balance.Services.Contracts;

/// <summary>
/// Lifecycle filter for the BankTransaction list view, per ADR 0012.
/// </summary>
public enum BankTransactionListFilter
{
    /// <summary>
    /// Rows with no referencing <c>JournalEntry</c> and no <c>DismissedAt</c>.
    /// Oldest-first by <c>BookingDate</c> — statement order, the categorization queue.
    /// </summary>
    Inbox = 0,

    /// <summary>
    /// Rows that already have a referencing <c>JournalEntry</c>. Newest-first (history view).
    /// </summary>
    Matched,

    /// <summary>
    /// Rows the user has dismissed (<c>DismissedAt IS NOT NULL</c>). Newest-dismissed first.
    /// </summary>
    Dismissed,

    /// <summary>
    /// Everything regardless of state. Newest-first by <c>BookingDate</c>.
    /// </summary>
    All,
}
