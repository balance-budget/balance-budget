using Microsoft.EntityFrameworkCore;
using PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;
using PhenX.EntityFrameworkCore.BulkInsert.Sqlite;
using Balance.Configuration.Options;

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
                    $"Data Source={DbPathHelper.GetDbPath()}",
                    x => x.MigrationsAssembly("Balance.Data.Sqlite")
                )
                .UseBulkInsertSqlite(),
            DatabaseProvider.Postgres => builder
                .UseNpgsql(
                    options.ConnectionString,
                    x => x.MigrationsAssembly("Balance.Data.PostgreSql")
                )
                .UseBulkInsertPostgreSql(),
            _ => throw new InvalidOperationException("Invalid database provider"),
        };
}
