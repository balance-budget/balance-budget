using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Web.Endpoints.JournalEntries;

internal sealed record JournalEntryResponse(
    JournalEntryId Id,
    DateOnly Date,
    string? Description,
    BankTransactionId? BankTransactionId,
    CounterpartyId? CounterpartyId,
    IReadOnlyList<JournalLineResponse> Lines,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public static JournalEntryResponse From(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        IReadOnlyList<JournalLineResponse> lines =
        [
            .. entry.Lines.Select(JournalLineResponse.From),
        ];
        return new JournalEntryResponse(
            entry.Id,
            entry.Date,
            entry.Description,
            entry.BankTransactionId,
            entry.CounterpartyId,
            lines,
            entry.CreatedAt,
            entry.UpdatedAt
        );
    }
}

internal sealed record JournalLineResponse(
    JournalLineId Id,
    AccountId AccountId,
    long Amount,
    ReconciliationStatus ReconciliationStatus,
    string? Description
)
{
    public static JournalLineResponse From(JournalLine line) =>
        new(line.Id, line.AccountId, line.Amount, line.ReconciliationStatus, line.Description);
}
