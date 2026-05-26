using Balance.Integration.Ing.Contracts;
using Balance.Integration.Ing.Importers;
using Balance.Integration.Ing.Parsers;
using Balance.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Integration.Ing;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceIntegrationIng(this IServiceCollection services) =>
        services
            .AddScoped<IIngNoteParser, IngNoteParser>()
            .AddScoped<IIngStatementParser, IngStatementParser>()
            .AddScoped<IngBankTransactionExtractor>()
            .AddScoped<IBankTransactionExtractor>(sp =>
                sp.GetRequiredService<IngBankTransactionExtractor>()
            );
}
