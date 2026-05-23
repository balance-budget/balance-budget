using System.Diagnostics;
using Balance.Configuration.Options;
using Balance.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Balance.Data.Helpers;

internal static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies provider-specific model conventions — currently the case-insensitive collation
    /// on the <c>Account</c> and <c>Counterparty</c> Name columns (SQLite NOCASE / Postgres
    /// citext). Both equality and pattern matching (LIKE / Contains / StartsWith) must behave
    /// identically across providers, anticipating typeahead search on these columns. NOCASE
    /// and citext deliver case-insensitive <c>=</c>, <c>LIKE</c>, and <c>ORDER BY</c> without
    /// per-query escape hatches.
    /// </summary>
    public static void ApplyProviderConventions(
        this ModelBuilder modelBuilder,
        DatabaseProvider provider
    )
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                modelBuilder.Entity<Account>().Property(a => a.Name).UseCollation("NOCASE");
                modelBuilder.Entity<Counterparty>().Property(c => c.Name).UseCollation("NOCASE");
                break;
            case DatabaseProvider.Postgres:
                modelBuilder.HasPostgresExtension("citext");
                modelBuilder.Entity<Account>().Property(a => a.Name).HasColumnType("citext");
                modelBuilder.Entity<Counterparty>().Property(c => c.Name).HasColumnType("citext");
                break;
            default:
                throw new UnreachableException($"Unknown DatabaseProvider '{provider}'.");
        }
    }
}
