using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Currencies;

/// <summary>
/// Cached lookup over the Currency reference table, warmed once at startup.
/// Used by Money for formatting/parsing and by services that need MinorUnitScale.
/// </summary>
public interface ICurrencyLookup
{
    Currency GetByCode(CurrencyCode code);

    Currency? TryGetByCode(CurrencyCode code);
}
