using Balance.Services.Contracts;
using Quartz;

namespace Balance.Services.Jobs;

internal class MaintenanceJob : IJob
{
    private readonly IDatabaseMaintenanceService _databaseMaintenanceService;

    public MaintenanceJob(IDatabaseMaintenanceService databaseMaintenanceService) =>
        _databaseMaintenanceService = databaseMaintenanceService;

    public async Task Execute(IJobExecutionContext context) =>
        await _databaseMaintenanceService.Optimize(context.CancellationToken);
}
