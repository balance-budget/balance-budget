using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// The read-side of the Outlook feature (ADR-0027): the computed-never-stored liquid-balance
/// <c>Projection</c>. Every call recomputes from current ledger balances, the user's
/// <c>JournalEntryTemplate</c>s, and the trailing-actuals <c>Typical spend</c> baseline; an
/// optional <see cref="OutlookScenarioInput"/> overlays an ephemeral what-if.
/// </summary>
public interface IOutlookService
{
    Task<Result<OutlookProjectionOutput>> GetProjectionAsync(
        CurrencyCode currencyCode,
        int horizonMonths,
        OutlookScenarioInput? scenario,
        CancellationToken cancellationToken
    );
}
