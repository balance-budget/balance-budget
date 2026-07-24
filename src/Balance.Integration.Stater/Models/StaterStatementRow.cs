namespace Balance.Integration.Stater.Models;

// Money-in-positive: a deposit-interest credit is positive; a settlement or draw is negative, so a
// settlement row's amount equals its deposit settlement leg (an asset credit).
internal sealed record StaterStatementRow(
    DateOnly Date,
    string Description,
    string? CounterpartyName,
    long AmountMinorUnits,
    string RawRecord
);
