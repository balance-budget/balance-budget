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
}
