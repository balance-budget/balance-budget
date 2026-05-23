using Balance.Data;
using Balance.Data.Helpers;
using Balance.Services.Contracts;
using Balance.Services.Logging;
using Microsoft.Extensions.Logging;

namespace Balance.Services.Jobs;

internal sealed class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
    private readonly ILogger<DatabaseMaintenanceService> _logger;
    private readonly BalanceDbContext _dbContext;

    public DatabaseMaintenanceService(
        ILogger<DatabaseMaintenanceService> logger,
        BalanceDbContext dbContext
    )
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task Optimize(CancellationToken cancellationToken)
    {
        _logger.DatabaseOptimizationStarted(DateTimeOffset.Now);

        // The commands below work for both SQLite and PostgreSQL

        // Run VACUUM command to shrink the database file size
        await _dbContext.Database.Vacuum(cancellationToken);

        // Run ANALYZE command to update the statistics for the database
        await _dbContext.Database.Analyze(cancellationToken);

        _logger.DatabaseOptimizationFinished(DateTimeOffset.Now);
    }
}
