using Microsoft.Extensions.Configuration;
using TUnit.AspNetCore;

namespace Balance.Tests.Api.Helpers;

internal abstract class EndpointsTestsBase : WebApplicationTest<WebApplicationFactory, Program>
{
    private TestSqliteDatabase _database = null!;

    protected override void ConfigureTestConfiguration(IConfigurationBuilder config)
    {
        _database ??= TestSqliteDatabase.Create(GetIsolatedName("balance"));
        config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:ConnectionString"] = _database.ConnectionString,
            }
        );
    }

    [After(Test)]
    public void CleanupDatabase() => _database.Dispose();
}
