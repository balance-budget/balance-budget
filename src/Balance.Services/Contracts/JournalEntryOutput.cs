using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Bookkeeping-shaped response for the <c>GET /api/journal-entries</c> list
/// endpoint. UI-shaped projections (net-worth-change, transfer detection,
/// from/to legs) are computed client-side per ADR-0011.
///
/// Name joins (<see cref="CounterpartyName"/> on the header and
/// <see cref="JournalLineOutput.AccountName"/> per line) are read-side ergonomics,
/// not UI projection — same pattern as <c>AccountOutput</c> joining a
/// <c>BankAccountSummary</c> per ADR-0007.
/// </summary>
public sealed record JournalEntryOutput(
    JournalEntryId Id,
    DateOnly Date,
    string? Description,
    CounterpartyId? CounterpartyId,
    string? CounterpartyName,
    IReadOnlyList<JournalLineOutput> Lines,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool HasBankTransactions
);

/// <summary>
/// Detail-endpoint shape: every <see cref="JournalEntryOutput"/> field plus the
/// <see cref="BankTransactions"/> list — zero, one, or (post-ADR-0012 Attach)
/// many imported rows that reference this entry. The cardinality lives on the
/// <c>BankTransaction</c> side now (<c>BankTransaction.JournalEntryId?</c>);
/// today the list is 0 or 1 elements long.
/// </summary>
public sealed record JournalEntryDetailOutput(
    JournalEntryId Id,
    DateOnly Date,
    string? Description,
    CounterpartyId? CounterpartyId,
    string? CounterpartyName,
    IReadOnlyList<JournalLineOutput> Lines,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<BankTransactionDetailOutput> BankTransactions
);

public sealed record JournalLineOutput(
    JournalLineId Id,
    AccountId AccountId,
    string AccountName,
    long Amount,
    ReconciliationStatus ReconciliationStatus,
    string? Description
);
