using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Detail shape for <c>GET/POST/PATCH /api/journal-entries/{id}</c>. Carries the same
/// projection fields as <see cref="JournalEntryRowOutput"/> (per ADR-0008 and ADR-0012)
/// plus the full <see cref="Lines"/> array. The SPA detail page consumes this single
/// response: the projection drives the personal-finance summary header, the lines
/// drive the bookkeeping table.
/// </summary>
public sealed record JournalEntryOutput(
    JournalEntryId Id,
    DateOnly Date,
    string? Description,
    BankTransactionId? BankTransactionId,
    CounterpartyId? CounterpartyId,
    string? CounterpartyName,
    int LineCount,
    bool IsTransfer,
    Money NetWorthChange,
    Money GrossMagnitude,
    bool IsSimplifiable,
    IReadOnlyList<JournalEntryLegSummary> FromLegs,
    IReadOnlyList<JournalEntryLegSummary> ToLegs,
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
