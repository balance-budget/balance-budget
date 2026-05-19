using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Balance.Services.Jobs;

internal static class JobsServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceJobs(this IServiceCollection services) =>
        services
            .AddQuartz(c =>
            {
                c.SchedulerName = "Balance Scheduler";
            })
            .AddQuartzHostedService(c =>
            {
                c.WaitForJobsToComplete = true;
                c.AwaitApplicationStarted = true;
            });
}
