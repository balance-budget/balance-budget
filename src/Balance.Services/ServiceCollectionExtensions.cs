using Balance.Configuration;
using Balance.Data;
using Balance.Services.Accounts;
using Balance.Services.BankAccounts;
using Balance.Services.Contracts;
using Balance.Services.Counterparties;
using Balance.Services.Currencies;
using Balance.Services.Jobs;
using FluentValidation;
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
            .AddValidatorsFromAssemblyContaining<IServicesAssemblyMarker>(
                ServiceLifetime.Singleton,
                includeInternalTypes: true
            )
            .AddScoped<ICurrencyService, CurrencyService>()
            .AddScoped<IAccountService, AccountService>()
            .AddScoped<ICounterpartyService, CounterpartyService>()
            .AddScoped<IBankAccountService, BankAccountService>()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IApplicationVersionService, ApplicationVersionService>();
    }
}
