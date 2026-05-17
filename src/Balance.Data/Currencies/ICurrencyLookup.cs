using System.Diagnostics.CodeAnalysis;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Currencies;

/// <summary>
/// Cached lookup over the Currency reference table, warmed once at startup.
/// Used by Money for formatting/parsing and by services that need MinorUnitScale.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Get / TryGet match the language used in the issue spec and are unambiguous here."
)]
public interface ICurrencyLookup
{
    Currency Get(CurrencyCode code);

    Currency? TryGet(CurrencyCode code);
}
