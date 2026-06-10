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
    IReadOnlyList<OutlookActualPointOutput> Actuals,
    IReadOnlyList<OutlookProjectedMonthOutput> Baseline,
    IReadOnlyList<OutlookProjectedMonthOutput>? Scenario
);

public sealed record OutlookActualPointOutput(DateOnly Month, long EndBalance);

/// <summary>
/// One projected month. <see cref="ExpectedNet"/> is the templates' net effect; <see
/// cref="TypicalSpendMid"/> the median everyday-spend applied; the <c>EndBalance*</c> trio is the
/// month-end balance band (Low pessimistic, High optimistic).
/// </summary>
public sealed record OutlookProjectedMonthOutput(
    DateOnly Month,
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
