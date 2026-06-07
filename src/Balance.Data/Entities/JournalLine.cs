using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class JournalLine : BaseEntity<JournalLineId>
{
    public required JournalEntryId JournalEntryId { get; set; }
    public required AccountId AccountId { get; set; }
    public required long Amount { get; set; }
    public ReconciliationStatus ReconciliationStatus { get; set; } = ReconciliationStatus.Uncleared;
    public string? Description { get; set; }

    /// <summary>
    /// Loan Part attribution, set only by loan-aware flows (ADR-0025). Principal lines are
    /// attributed intrinsically by their Account; interest lines (posted to the Loan's interest
    /// Expense account) explicitly, so per-part interest is a pure ledger query.
    /// </summary>
    public LoanPartId? LoanPartId { get; set; }
}
