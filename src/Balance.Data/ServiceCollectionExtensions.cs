using Balance.Configuration.Helpers;
using Balance.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
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
        services.AddDbContext<BalanceDbContext>();

        var builder = services.AddDataProtection().SetApplicationName("Balance");

        // Fetching data protection keys from the database breaks design time runs
        if (!environment.IsDesignTime())
            builder.PersistKeysToDbContext<BalanceDbContext>();

        // Identity Core only — no RoleManager (ADR 0015 has no roles), no UI. The cookie /
        // policy scheme and API token handler live in Balance.Web.
        services
            .AddIdentityCore<BalanceUser>(options =>
            {
                // Length-first password policy (ADR 0016), per NIST SP 800-63B.
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<BalanceDbContext>();

        return services;
    }
}
