using Quartz;

namespace Balance.Services.Jobs;

internal static class ServiceCollectionQuartzConfiguratorExtensions
{
    public static IQuartzBuilder ScheduleJobAt<TJob>(this IQuartzBuilder builder, string schedule)
        where TJob : IJob =>
        builder.ScheduleJob<TJob>(
            t => t.StartNow().WithCronSchedule(schedule),
            j => j.WithIdentity(typeof(TJob).Name).DisallowConcurrentExecution()
        );
}
