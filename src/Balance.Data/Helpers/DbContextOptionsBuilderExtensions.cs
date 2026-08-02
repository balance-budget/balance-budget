using System.Diagnostics;
using Balance.Configuration.Options;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;
using PhenX.EntityFrameworkCore.BulkInsert.Sqlite;

namespace Balance.Data.Helpers;

internal static class DbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseProvider(
        this DbContextOptionsBuilder builder,
        DatabaseOptions options
    ) =>
        options.Provider switch
        {
            DatabaseProvider.Sqlite => builder
                .UseSqlite(
                    string.IsNullOrWhiteSpace(options.ConnectionString)
                        ? $"Data Source={DbPathHelper.GetDbPath()}"
                        : options.ConnectionString,
                    x => x.MigrationsAssembly("Balance.Data.Sqlite")
                )
                .UseBulkInsertSqlite()
                .UseSqliteExceptionProcessor(),
            DatabaseProvider.Postgres => builder
                .UseNpgsql(
                    DisablePostgresGssEncryption(options.ConnectionString),
                    x => x.MigrationsAssembly("Balance.Data.PostgreSql")
                )
                .UseBulkInsertPostgreSql()
                .UsePostgresExceptionProcessor(),
            _ => throw new UnreachableException($"Unknown DatabaseProvider '{options.Provider}'."),
        };

    private static DbContextOptionsBuilder UseSqliteExceptionProcessor(
        this DbContextOptionsBuilder builder
    ) =>
        EntityFramework.Exceptions.Sqlite.ExceptionProcessorExtensions.UseExceptionProcessor(
            builder
        );

    private static DbContextOptionsBuilder UsePostgresExceptionProcessor(
        this DbContextOptionsBuilder builder
    ) =>
        EntityFramework.Exceptions.PostgreSQL.ExceptionProcessorExtensions.UseExceptionProcessor(
            builder
        );

    /// <summary>
    /// Npgsql tries to use GSSAPI encryption. This requires libgssapi_krb5 to be installed.
    /// It also leads to a low-level memory corruption error in libgssapi_krb5 when opening many parallel connections.
    /// Disable it by default for all PostgreSQL connections
    /// </summary>
    private static string DisablePostgresGssEncryption(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            GssEncryptionMode = GssEncryptionMode.Disable,
        };
        return builder.ConnectionString;
    }
}
