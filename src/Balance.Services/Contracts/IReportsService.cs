using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// The Insights reporting projections (see CONTEXT.md). Both reports are scoped to an inclusive
/// <b>Reporting period</b> <c>[from, to]</c> over <see cref="Balance.Data.Entities.JournalEntry"/>
/// dates and a single currency; no cross-currency conversion happens here.
/// </summary>
public interface IReportsService
{
    Task<Result<DistributionOutput>> GetDistributionAsync(
        DistributionType type,
        AccountId? parentAccountId,
        DateOnly fromDate,
        DateOnly toDate,
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    );

    /// <param name="maxDepth">
    /// Caps how many category levels below the hub are drawn: 1 shows only the chart-of-accounts
    /// roots (each collapsing its whole subtree), 2 shows roots plus one sub-level, and so on.
    /// <c>null</c> draws the full hierarchy. Capping only folds deeper nodes into their ancestor at
    /// the cutoff, so the double-entry balance is preserved at any depth.
    /// </param>
    Task<Result<MoneyFlowOutput>> GetMoneyFlowAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CurrencyCode currencyCode,
        int? maxDepth,
        CancellationToken cancellationToken
    );
}
