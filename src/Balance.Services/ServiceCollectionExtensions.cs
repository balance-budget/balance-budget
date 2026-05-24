using Balance.Configuration;
using Balance.Configuration.Helpers;
using Balance.Data;
using Balance.Services.Accounts;
using Balance.Services.BankAccounts;
using Balance.Services.BankTransactions;
using Balance.Services.Contracts;
using Balance.Services.Counterparties;
using Balance.Services.Currencies;
using Balance.Services.Dashboard;
using Balance.Services.Jobs;
using Balance.Services.JournalEntries;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Balance.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Only start the job scheduler when running in real environment
        if (!environment.IsIntegrationTest() && !environment.IsDesignTime())
            services.AddBalanceJobs(configuration);

        return services
            .AddBalanceConfiguration(configuration)
            .AddBalanceData(configuration, environment)
            .AddMemoryCache()
            .AddValidatorsFromAssemblyContaining<IServicesAssemblyMarker>(
                includeInternalTypes: true
            )
            .AddScoped<ICurrencyService, CurrencyService>()
            .AddScoped<IAccountService, AccountService>()
            .AddScoped<IAccountBalanceService, AccountBalanceService>()
            .AddScoped<IRegisterService, RegisterService>()
            .AddScoped<ICounterpartyService, CounterpartyService>()
            .AddScoped<IBankAccountService, BankAccountService>()
            .AddScoped<IBankTransactionService, BankTransactionService>()
            .AddScoped<IBankTransactionImportService, BankTransactionImportService>()
            .AddScoped<IJournalEntryService, JournalEntryService>()
            .AddScoped<IDashboardService, DashboardService>()
            .AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IApplicationVersionService, ApplicationVersionService>();
    }
}
