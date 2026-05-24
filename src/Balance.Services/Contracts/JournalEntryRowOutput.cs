using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// UI-shaped row for the <c>/journal</c> overview (ADR-0008). The projection fields
/// (<see cref="IsTransfer"/>, <see cref="NetWorthChange"/>, <see cref="GrossMagnitude"/>,
/// <see cref="IsSimplifiable"/>, <see cref="FromLegs"/>, <see cref="ToLegs"/>) are
/// computed server-side per ADR-0012 so the client never reaches into lines to
/// recompute the sign or shape.
/// </summary>
public sealed record JournalEntryRowOutput(
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
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record JournalEntryLegSummary(AccountId AccountId, string AccountName);
