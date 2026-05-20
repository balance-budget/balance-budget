using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record RegisterRowOutput(
    JournalEntryId JournalEntryId,
    JournalLineId JournalLineId,
    DateOnly Date,
    string? EntryDescription,
    CounterpartyId? CounterpartyId,
    string? CounterpartyName,
    string? LineDescription,
    ReconciliationStatus ReconciliationStatus,
    Money Amount,
    IReadOnlyList<RegisterRowCounterLeg> Counter
);

public sealed record RegisterRowCounterLeg(AccountId AccountId, string AccountName, Money Amount);
