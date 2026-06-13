using Balance.Tests.Api.Helpers;
using Microsoft.Extensions.Configuration;
using TUnit.AspNetCore;

namespace Balance.Tests.Api.Auth;

internal abstract class RealAuthEndpointsTestsBase
    : WebApplicationTest<RealAuthWebApplicationFactory, Program>
{
    private TestSqliteDatabase _database = null!;

    protected override void ConfigureTestConfiguration(IConfigurationBuilder config)
    {
        _database ??= TestSqliteDatabase.Create(GetIsolatedName("balance-auth"));
        config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:ConnectionString"] = _database.ConnectionString,
                ["Auth:SetupToken"] = RealAuthWebApplicationFactory.SetupToken,
                ["Auth:CookieSecure"] = "false",
                ["Auth:CookieSameSite"] = "Lax",
            }
        );
    }

    [After(Test)]
    public void CleanupDatabase() => _database.Dispose();
}
