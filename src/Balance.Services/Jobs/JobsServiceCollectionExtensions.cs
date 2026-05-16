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
        IConfiguration configuration,
        bool start
    )
    {
        var options = configuration.GetSection<JobsOptions>();
        if (!options.RunJobs)
            return services;

        return services
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
}
