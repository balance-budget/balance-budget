using Quartz;

namespace Balance.Services.Jobs;

internal static class ServiceCollectionQuartzConfiguratorExtensions
{
    public static IServiceCollectionQuartzConfigurator ScheduleJobAt<TJob>(
        this IServiceCollectionQuartzConfigurator configurator,
        string schedule
    )
        where TJob : IJob =>
        configurator.ScheduleJob<TJob>(
            t => t.StartNow().WithCronSchedule(schedule),
            j => j.WithIdentity(nameof(TJob)).DisallowConcurrentExecution()
        );
}
