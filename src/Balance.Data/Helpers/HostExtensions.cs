using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Balance.Data.Logging;

namespace Balance.Data.Helpers;

public static class HostExtensions
{
    public static async Task MigrateDatabase(this IHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);

        await using var scope = host.Services.CreateAsyncScope();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IHost>>();
        logger.DatabaseMigrationStarted();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.DatabaseMigrationFinished();
    }
}
