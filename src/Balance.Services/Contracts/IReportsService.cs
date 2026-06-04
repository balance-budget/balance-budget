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

    /// <param name="expandedAccountIds">
    /// The set of accounts whose children should be drawn. Every root renders as a node; the
    /// recursion descends into a node only when its id is in this set, otherwise the node collapses
    /// (its whole subtree folds into it). An empty set therefore draws roots only. Folding only
    /// moves value into the collapsed ancestor, so the double-entry balance is preserved at any
    /// expansion. Ids not on a visible path (dormant, wrong currency, orphaned) are harmlessly
    /// ignored — they are simply never reached.
    /// </param>
    Task<Result<MoneyFlowOutput>> GetMoneyFlowAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CurrencyCode currencyCode,
        IReadOnlySet<AccountId> expandedAccountIds,
        CancellationToken cancellationToken
    );
}
