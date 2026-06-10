namespace Balance.Data.Entities.Enums;

/// <summary>
/// The repetition rhythm of a <see cref="JournalEntryTemplate"/> (CONTEXT.md, ADR-0027). A closed
/// set; <see cref="Once"/> is the planned one-off (a future insurance bill, a planned purchase),
/// every other value a recurring item. There is no general recurrence expression in v1.
/// </summary>
public enum Cadence
{
    Once,
    Weekly,
    Monthly,
    Quarterly,
    Yearly,
}
