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

    Task<Result<MoneyFlowOutput>> GetMoneyFlowAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    );
}
