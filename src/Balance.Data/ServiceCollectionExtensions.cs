using Balance.Configuration.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Balance.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceData(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddDbContextFactory<BalanceDbContext>();
        // Pooled context — ctor deps are all singleton-shaped, OnConfiguring runs on each rent.
        services.AddDbContextPool<BalanceDbContext>(_ => { });

        var builder = services.AddDataProtection().SetApplicationName("Balance");

        // Fetching data protection keys from the database breaks design time runs
        if (!environment.IsDesignTime())
            builder.PersistKeysToDbContext<BalanceDbContext>();

        return services;
    }
}
