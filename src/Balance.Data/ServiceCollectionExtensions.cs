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
        // Pooled context — ctor deps are all singleton-shaped, OnConfiguring runs on each rent.
        services.AddDbContextPool<BalanceDbContext>(_ => { });
        services
            .AddDataProtection()
            .SetApplicationName("Balance")
            .PersistKeysToDbContext<BalanceDbContext>();
        return services;
    }
}
