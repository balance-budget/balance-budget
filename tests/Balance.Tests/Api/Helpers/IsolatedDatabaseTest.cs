using Microsoft.Extensions.Configuration;
using TUnit.AspNetCore;

namespace Balance.Tests.Api.Helpers;

/// <summary>
/// Base for integration tests that boot the real host. Each test gets its own uniquely-named
/// in-memory SQLite database wired through configuration, so tests never share state and the host
/// never falls back to the shared on-disk database (where parallel hosts race each other in
/// <c>EnsureCreatedAsync</c> with "table already exists"). Derived classes choose the factory via
/// <typeparamref name="TFactory"/> and add extra settings through
/// <see cref="ConfigureAdditionalSettings"/>.
/// </summary>
internal abstract class IsolatedDatabaseTest<TFactory> : WebApplicationTest<TFactory, Program>
    where TFactory : TestWebApplicationFactory<Program>, new()
{
    private TestSqliteDatabase? _database;

    protected override void ConfigureTestConfiguration(IConfigurationBuilder config)
    {
        // GetIsolatedName appends a per-test discriminator, so the base name is just a label and
        // every test still gets its own database.
        _database ??= TestSqliteDatabase.Create(GetIsolatedName("balance"));
        var settings = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = _database.ConnectionString,
        };
        ConfigureAdditionalSettings(settings);
        config.AddInMemoryCollection(settings);
    }

    /// <summary>Hook for derived tests to contribute extra configuration alongside the database.</summary>
    protected virtual void ConfigureAdditionalSettings(IDictionary<string, string?> settings) { }

    [After(Test)]
    public void CleanupDatabase() => _database?.Dispose();
}
