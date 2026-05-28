using Microsoft.Extensions.Configuration;
using TUnit.AspNetCore;

namespace Balance.Tests.Api.Auth;

internal abstract class RealAuthEndpointsTestsBase
    : WebApplicationTest<RealAuthWebApplicationFactory, Program>
{
    private string _dbPath = null!;

    protected override void ConfigureTestConfiguration(IConfigurationBuilder config)
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{GetIsolatedName("balance-auth")}.db");
        config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:ConnectionString"] = $"Data Source={_dbPath}",
                ["Auth:SetupToken"] = RealAuthWebApplicationFactory.SetupToken,
                ["Auth:CookieSecure"] = "false",
                ["Auth:CookieSameSite"] = "Lax",
            }
        );
    }

    [After(Test)]
    public void CleanupDatabase()
    {
        foreach (var path in new[] { _dbPath, $"{_dbPath}-shm", $"{_dbPath}-wal" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
