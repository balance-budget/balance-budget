using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceData(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddDbContextFactory<BalanceDbContext>();
        services.AddDbContext<BalanceDbContext>();
        services
            .AddDataProtection()
            .SetApplicationName("Balance")
            .PersistKeysToDbContext<BalanceDbContext>();
        return services;
    }
}
