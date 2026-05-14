using Balance.Configuration.Contracts;
using Balance.Configuration.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceConfiguration(
        this IServiceCollection services,
        IConfiguration configuration
    ) => services.AddSettings<DatabaseOptions>(configuration);

    private static IServiceCollection AddSettings<T>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where T : class, IOptionsSection
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<T>(configuration.GetSection(T.Section));
        return services;
    }
}
