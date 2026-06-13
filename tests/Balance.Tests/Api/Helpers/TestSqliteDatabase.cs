using Microsoft.Data.Sqlite;

namespace Balance.Tests.Api.Helpers;

/// <summary>
/// A per-test SQLite database that lives entirely in memory. A uniquely-named shared-cache
/// in-memory database exists only while at least one connection to it stays open, so the test
/// holds this keep-alive connection for its lifetime and disposes it when done, which drops the
/// database. The application under test opens its own connections to the same name through the
/// connection string and shares the data (<c>Cache=Shared</c>). Pooling is disabled so the
/// database is released deterministically rather than lingering in the connection pool.
/// </summary>
internal sealed class TestSqliteDatabase : IDisposable
{
    private readonly SqliteConnection _keepAlive;

    private TestSqliteDatabase(string connectionString)
    {
        ConnectionString = connectionString;
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();
    }

    public string ConnectionString { get; }

    public static TestSqliteDatabase Create(string name) =>
        new($"Data Source={name};Mode=Memory;Cache=Shared;Pooling=False");

    public void Dispose() => _keepAlive.Dispose();
}
