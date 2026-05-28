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
            .AddScoped<IIngCurrentAccountStatementParser, IngCurrentAccountStatementParser>()
            .AddScoped<IIngCreditCardStatementParser, IngCreditCardStatementParser>()
            .AddScoped<IBankTransactionExtractor, IngBankTransactionExtractor>()
            .AddScoped<IBankTransactionExtractor, IngCreditCardTransactionExtractor>();
}
