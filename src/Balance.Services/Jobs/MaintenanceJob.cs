using Balance.Services.Contracts;
using Quartz;

namespace Balance.Services.Jobs;

internal sealed class MaintenanceJob : IJob
{
    private readonly IDatabaseMaintenanceService _databaseMaintenanceService;

    public MaintenanceJob(IDatabaseMaintenanceService databaseMaintenanceService) =>
        _databaseMaintenanceService = databaseMaintenanceService;

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct) =>
        await _databaseMaintenanceService.Optimize(ct);
}
