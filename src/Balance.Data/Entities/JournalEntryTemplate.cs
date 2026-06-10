using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

/// <summary>
/// A user-confirmed, <see cref="JournalEntry"/>-<em>shaped</em> pattern that drives the
/// forward-looking <c>Projection</c> in the <c>Outlook</c> section (ADR-0027). It is <em>not</em> a
/// <see cref="JournalEntry"/> and is never posted: the future is computed, never written, so the
/// ledger gains no columns for it. This is the only new stored concept in the feature.
/// </summary>
public sealed class JournalEntryTemplate : BaseEntity<JournalEntryTemplateId>
{
    public required string Name { get; set; }

    /// <summary>
    /// The pinned Liquid balance-sheet <see cref="Account"/> (the bank-side leg — the checking or
    /// savings account this template moves money in or out of). The account whose balance
    /// <c>Projection</c> the template feeds.
    /// </summary>
    public required AccountId AccountId { get; set; }

    /// <summary>
    /// The descriptive counter-side <see cref="Account"/> (an Expense or Income account, usually) —
    /// optional, purely for display and as a pre-fill hint; the projection only needs the pinned
    /// account leg.
    /// </summary>
    public AccountId? CounterAccountId { get; set; }

    /// <summary>The real-world party, when known. Part of the fallback matching key.</summary>
    public CounterpartyId? CounterpartyId { get; set; }

    public required Cadence Cadence { get; set; }

    /// <summary>
    /// For <see cref="Cadence.Once"/> the single planned date; for a recurring cadence the anchor
    /// (first) occurrence. Its day-of-month is the nominal day later occurrences land on (the real
    /// charge drifts within a window — see <c>Occurrence matching</c>).
    /// </summary>
    public required DateOnly AnchorDate { get; set; }

    /// <summary>Optional end date; the template is open-ended when null. No occurrence-count limit.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// The expected amount as a <em>raw ledger-signed</em> <see cref="JournalLine.Amount"/> on the
    /// pinned <see cref="AccountId"/> (debit positive, credit negative — the same Sign convention as
    /// the ledger), in minor units of the pinned account's currency. Seeded from history on
    /// detection, then frozen until the user edits it (never silent drift).
    /// </summary>
    public required long ExpectedAmount { get; set; }

    /// <summary>
    /// SEPA mandate id, when the detected occurrences carried one — the precise half of the layered
    /// matching key (ADR-0027). Null for salary credits, standing orders, manual rent, etc.
    /// </summary>
    public string? MandateId { get; set; }

    /// <summary>SEPA creditor id, the other precise matching signal when present.</summary>
    public string? SepaCreditorId { get; set; }
}
