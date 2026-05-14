using Balance.Configuration;
using Balance.Data;
using Balance.Services.Contracts;
using Balance.Services.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool startJobs = true
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return services
            .AddBalanceConfiguration(configuration)
            .AddBalanceData(configuration)
            .AddBalanceJobs(configuration, startJobs)
            .AddSingleton<IApplicationVersionService, ApplicationVersionService>();
    }
}
