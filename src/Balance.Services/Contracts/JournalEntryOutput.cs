using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record JournalEntryOutput(
    JournalEntryId Id,
    DateOnly Date,
    string? Description,
    BankTransactionId? BankTransactionId,
    CounterpartyId? CounterpartyId,
    IReadOnlyList<JournalLineOutput> Lines,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record JournalLineOutput(
    JournalLineId Id,
    AccountId AccountId,
    long Amount,
    ReconciliationStatus ReconciliationStatus,
    string? Description
);
