using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Read shape for a <c>JournalEntryTemplate</c> (ADR-0027). <see cref="ExpectedAmount"/> is the raw
/// ledger-signed amount on the pinned account (debit +, credit −); <see cref="MonthlyEquivalent"/>
/// is the balance-normalized per-month average (inflow +, outflow −) for the list's "≈ /mo" figure;
/// <see cref="NextDueDate"/> is the next occurrence on or after today, null when the template has
/// ended. All money is minor units of <see cref="CurrencyCode"/>.
/// </summary>
public sealed record JournalEntryTemplateOutput(
    JournalEntryTemplateId Id,
    string Name,
    AccountId AccountId,
    string AccountName,
    AccountId? CounterAccountId,
    string? CounterAccountName,
    CounterpartyId? CounterpartyId,
    string? CounterpartyName,
    Cadence Cadence,
    DateOnly AnchorDate,
    DateOnly? EndDate,
    long ExpectedAmount,
    long MonthlyEquivalent,
    DateOnly? NextDueDate,
    CurrencyCode CurrencyCode,
    string? MandateId,
    string? SepaCreditorId
);

public sealed record CreateJournalEntryTemplateInput(
    string Name,
    AccountId AccountId,
    AccountId? CounterAccountId,
    CounterpartyId? CounterpartyId,
    Cadence Cadence,
    DateOnly AnchorDate,
    DateOnly? EndDate,
    long ExpectedAmount,
    string? MandateId,
    string? SepaCreditorId
);

public sealed record UpdateJournalEntryTemplateInput(
    string Name,
    AccountId AccountId,
    AccountId? CounterAccountId,
    CounterpartyId? CounterpartyId,
    Cadence Cadence,
    DateOnly AnchorDate,
    DateOnly? EndDate,
    long ExpectedAmount
);

/// <summary>
/// A detected, not-yet-confirmed recurring pattern mined from history (ADR-0027 hybrid model) —
/// surfaced for the user to accept (creating a real <c>JournalEntryTemplate</c>) or dismiss. Never
/// stored; recomputed on demand. <see cref="ExpectedAmount"/> is seeded from the median of the
/// matched occurrences; <see cref="OccurrenceCount"/> is how many supported the detection.
/// </summary>
public sealed record TemplateCandidateOutput(
    AccountId AccountId,
    string AccountName,
    AccountId? CounterAccountId,
    string? CounterAccountName,
    CounterpartyId? CounterpartyId,
    string? CounterpartyName,
    string SuggestedName,
    Cadence Cadence,
    DateOnly AnchorDate,
    long ExpectedAmount,
    long MonthlyEquivalent,
    int OccurrenceCount,
    CurrencyCode CurrencyCode,
    string? MandateId,
    string? SepaCreditorId
);
