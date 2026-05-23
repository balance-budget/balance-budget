using System.Diagnostics;
using Balance.Configuration.Options;
using Microsoft.EntityFrameworkCore;
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
                    options.ConnectionString,
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
}
