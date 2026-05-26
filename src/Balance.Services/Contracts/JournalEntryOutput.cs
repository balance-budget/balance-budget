using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Bookkeeping-shaped response for the <c>GET /api/journal-entries</c> list
/// endpoint. UI-shaped projections (net-worth-change, transfer detection,
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

/// <summary>
/// Detail-endpoint shape: every <see cref="JournalEntryOutput"/> field plus the
/// linked <see cref="BankTransactionDetailOutput"/> (when the entry was created
/// from a bank import). Returned from <c>GET /api/journal-entries/{id}</c>,
/// <c>POST</c>, <c>PATCH</c>, and the categorize endpoint — same split as
/// <c>BankTransactionOutput</c> vs <c>BankTransactionDetailOutput</c>.
/// </summary>
public sealed record JournalEntryDetailOutput(
    JournalEntryId Id,
    DateOnly Date,
    string? Description,
    BankTransactionId? BankTransactionId,
    CounterpartyId? CounterpartyId,
    string? CounterpartyName,
    IReadOnlyList<JournalLineOutput> Lines,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    BankTransactionDetailOutput? BankTransaction
);

public sealed record JournalLineOutput(
    JournalLineId Id,
    AccountId AccountId,
    string AccountName,
    long Amount,
    ReconciliationStatus ReconciliationStatus,
    string? Description
);
