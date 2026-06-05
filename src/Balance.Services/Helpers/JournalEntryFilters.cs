using System.Linq.Expressions;
using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Helpers;

namespace Balance.Services.Helpers;

internal static class JournalEntryFilters
{
    /// <summary>
    /// The shared in-list <c>?q=</c> predicate: a JournalEntry matches when its Description or its
    /// linked Counterparty's Name contains <paramref name="needle"/> (case-insensitive LIKE). Single
    /// source for the Activity/journal-entries list and the per-Account Register so the two filters
    /// can't drift — see ADR-0017 item (g) and its amendment.
    /// </summary>
    public static Expression<Func<JournalEntry, bool>> MatchesNeedle(
        BalanceDbContext dbContext,
        string needle
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        var pattern = $"%{needle}%";
        return e =>
            (e.Description != null && DbFunction.CaseInsensitiveLike(e.Description, pattern))
            || (
                e.CounterpartyId != null
                && dbContext.Counterparties.Any(c =>
                    c.Id == e.CounterpartyId && DbFunction.CaseInsensitiveLike(c.Name, pattern)
                )
            );
    }
}
