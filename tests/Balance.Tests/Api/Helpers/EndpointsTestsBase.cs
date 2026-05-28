using Microsoft.Extensions.Configuration;
using TUnit.AspNetCore;

namespace Balance.Tests.Api.Helpers;

internal abstract class EndpointsTestsBase : WebApplicationTest<WebApplicationFactory, Program>
{
    private string _dbPath = null!;

    protected override void ConfigureTestConfiguration(IConfigurationBuilder config)
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{GetIsolatedName("balance")}.db");
        Console.WriteLine(_dbPath);
        config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:ConnectionString"] = $"Data Source={_dbPath}",
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
