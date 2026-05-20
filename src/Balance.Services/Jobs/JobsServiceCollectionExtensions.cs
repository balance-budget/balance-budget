using Balance.Configuration.Helpers;
using Balance.Configuration.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Balance.Services.Jobs;

internal static class JobsServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceJobs(
        this IServiceCollection services,
        IConfiguration configuration
    ) =>
        services
            .AddQuartz(c =>
            {
                var config = configuration.GetSection<JobsOptions>();
                c.SchedulerName = "Balance Scheduler";
                c.ScheduleJobAt<MaintenanceJob>(config.MaintenanceSchedule);
            })
            .AddQuartzHostedService(c =>
            {
                c.WaitForJobsToComplete = true;
                c.AwaitApplicationStarted = true;
            });
}
