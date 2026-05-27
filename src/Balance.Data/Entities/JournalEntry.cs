using System.Collections.ObjectModel;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class JournalEntry : BaseEntity<JournalEntryId>
{
    public required DateOnly Date { get; set; }
    public string? Description { get; set; }
    public CounterpartyId? CounterpartyId { get; set; }

    public Collection<JournalLine> Lines { get; } = [];
}
