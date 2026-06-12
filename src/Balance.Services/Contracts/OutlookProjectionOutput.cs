using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// The forward-looking <c>Projection</c> for the liquid balance-sheet accounts (ADR-0027): per
/// account, the past month-end balances (ledger actuals, left of the anchor) plus the projected
/// future months (right of the anchor), with an optional what-if <c>Scenario</c> overlaid. All
/// money is minor units, balance-normalized to each account's own perspective.
/// </summary>
public sealed record OutlookProjectionOutput(
    DateOnly AnchorMonth,
    int HorizonMonths,
    IReadOnlyList<OutlookAccountProjectionOutput> Accounts
);

public sealed record OutlookAccountProjectionOutput(
    AccountId AccountId,
    string AccountName,
    AccountType AccountType,
    CurrencyCode CurrencyCode,
    long CurrentBalance,
    OutlookThisMonthOutput ThisMonth,
    OutlookYearEndOutput YearEnd,
    IReadOnlyList<OutlookActualPointOutput> Actuals,
    IReadOnlyList<OutlookProjectedMonthOutput> Baseline,
    IReadOnlyList<OutlookProjectedMonthOutput>? Scenario
);

public sealed record OutlookActualPointOutput(DateOnly Month, long EndBalance);

/// <summary>
/// The "rest of this month" summary (ADR-0028): recurring money still expected to come in / go out
/// before month-end (shown exactly), the everyday-spend range still ahead, and the resulting
/// end-of-month balance band. <see cref="ExpectedIn"/> is ≥ 0, <see cref="ExpectedOut"/> ≤ 0, and
/// the <c>EverydaySpend*</c> bounds are ≤ 0 and prorated to the remaining days.
/// </summary>
public sealed record OutlookThisMonthOutput(
    DateOnly Month,
    long ExpectedIn,
    long ExpectedOut,
    long EverydaySpendLow,
    long EverydaySpendHigh,
    long EndBalanceLow,
    long EndBalanceMid,
    long EndBalanceHigh
);

/// <summary>
/// The projected balance band on 31 December of the current year (ADR-0028) for the account,
/// computed by projecting through December regardless of the requested horizon.
/// </summary>
public sealed record OutlookYearEndOutput(
    DateOnly Date,
    long EndBalanceLow,
    long EndBalanceMid,
    long EndBalanceHigh
);

/// <summary>
/// One projected month. <see cref="ExpectedIn"/> / <see cref="ExpectedOut"/> split the templates'
/// effect by direction (<see cref="ExpectedNet"/> is their sum); <see cref="TypicalSpendMid"/> the
/// median everyday-spend applied; the <c>EndBalance*</c> trio is the month-end balance band (Low
/// pessimistic, High optimistic).
/// </summary>
public sealed record OutlookProjectedMonthOutput(
    DateOnly Month,
    long ExpectedIn,
    long ExpectedOut,
    long ExpectedNet,
    long TypicalSpendMid,
    long EndBalanceLow,
    long EndBalanceMid,
    long EndBalanceHigh
);

/// <summary>
/// An ephemeral what-if overlay (ADR-0027); never persisted. The three v1 levers: disable existing
/// templates, add hypothetical ones, and override an existing template's expected amount.
/// </summary>
public sealed record OutlookScenarioInput(
    IReadOnlyList<JournalEntryTemplateId> DisabledTemplateIds,
    IReadOnlyList<OutlookScenarioTemplateInput> AddedTemplates,
    IReadOnlyList<OutlookScenarioAmountOverrideInput> AmountOverrides
);

public sealed record OutlookScenarioTemplateInput(
    AccountId AccountId,
    Cadence Cadence,
    DateOnly AnchorDate,
    DateOnly? EndDate,
    long ExpectedAmount
);

public sealed record OutlookScenarioAmountOverrideInput(
    JournalEntryTemplateId TemplateId,
    long ExpectedAmount
);
