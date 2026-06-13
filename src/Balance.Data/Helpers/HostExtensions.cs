using Balance.Configuration.Helpers;
using Balance.Data.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Balance.Data.Helpers;

public static class HostExtensions
{
    public static async Task MigrateDatabaseAsync(
        this IHost host,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(host);
        var environment = host.Services.GetRequiredService<IHostEnvironment>();

        // Don't run migrations during design time runs
        if (environment.IsDesignTime())
            return;

        await using var scope = host.Services.CreateAsyncScope();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IHost>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();

        // Integration tests boot a fresh database per test, so replaying the full migration
        // history (and growing with every new migration) is pure overhead. EnsureCreated builds
        // the same schema from the current model in a single pass. The migrations' only raw SQL
        // is data backfills against existing rows, which are no-ops on an empty database, so the
        // resulting schema is identical. Real hosts keep MigrateAsync to apply history in order.
        if (environment.IsIntegrationTest())
        {
            logger.DatabaseSchemaEnsured();
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        logger.DatabaseMigrationStarted();
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.DatabaseMigrationFinished();
    }
}
