using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Bookkeeping-shaped response for every <c>GET/POST/PATCH /api/journal-entries</c>
/// endpoint (list, detail, create, update). The same shape backs the list and
/// detail responses; UI-shaped projections (net-worth-change, transfer detection,
/// from/to legs) are computed client-side per ADR-0012.
///
/// Name joins (<see cref="CounterpartyName"/> on the header and
/// <see cref="JournalLineOutput.AccountName"/> per line) are read-side ergonomics,
/// not UI projection — same pattern as <c>AccountOutput</c> joining a
/// <c>BankAccountSummary</c> per ADR-0008.
/// </summary>
public sealed record JournalEntryOutput(
    JournalEntryId Id,
    DateOnly Date,
    string? Description,
    BankTransactionId? BankTransactionId,
    CounterpartyId? CounterpartyId,
    string? CounterpartyName,
    IReadOnlyList<JournalLineOutput> Lines,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record JournalLineOutput(
    JournalLineId Id,
    AccountId AccountId,
    string AccountName,
    long Amount,
    ReconciliationStatus ReconciliationStatus,
    string? Description
);
