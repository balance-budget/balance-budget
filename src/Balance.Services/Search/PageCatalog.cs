using Balance.Services.Contracts;

namespace Balance.Services.Search;

/// <summary>
/// Static list of routes the SPA exposes as top-level destinations. Mirrors the
/// sidebar's NAV_MAIN / NAV_PLAN / NAV_OTHER groups; the launcher matches a
/// trimmed query against each entry's label and keyword aliases so e.g. "tx"
/// finds Bank transactions.
/// </summary>
internal static class PageCatalog
{
    private static readonly IReadOnlyList<PageEntry> Entries =
    [
        new PageEntry("Dashboard", "/", ["home", "overview"]),
        new PageEntry("Accounts", "/accounts", ["ledger"]),
        new PageEntry("Journal", "/journal", ["entries", "ledger"]),
        new PageEntry(
            "Bank transactions",
            "/bank-transactions",
            ["inbox", "bt", "tx", "transactions"]
        ),
        new PageEntry("Bank imports", "/bank-imports", ["import", "upload", "statement"]),
        new PageEntry("Budgets", "/budgets", []),
        new PageEntry("Subscriptions", "/subscriptions", []),
        new PageEntry("Piggy banks", "/piggy-banks", ["savings", "goals"]),
        new PageEntry("Reports", "/reports", []),
        new PageEntry("Settings", "/settings", []),
        new PageEntry("Settings · Bank accounts", "/settings/bank-accounts", ["iban", "ledger"]),
        new PageEntry("Settings · Counterparties", "/settings/counterparties", ["payees"]),
        new PageEntry("Settings · Users", "/settings/users", ["accounts", "people"]),
        new PageEntry("Settings · Tokens", "/settings/tokens", ["api", "pat"]),
    ];

    public static IEnumerable<PageHit> Match(string needle)
    {
        // Case-insensitive substring against the label or any keyword. Order is
        // intentional: declaration order matches the sidebar so primary
        // destinations rank above settings sub-pages.
        return Entries
            .Where(e =>
                e.Label.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || e.Keywords.Any(k => k.Contains(needle, StringComparison.OrdinalIgnoreCase))
            )
            .Select(e => new PageHit(e.Label, e.Route));
    }

    private sealed record PageEntry(string Label, string Route, IReadOnlyList<string> Keywords);
}
