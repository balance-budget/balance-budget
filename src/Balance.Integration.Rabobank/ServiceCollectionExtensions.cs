using Balance.Integration.Rabobank.Contracts;
using Balance.Integration.Rabobank.Importers;
using Balance.Integration.Rabobank.Parsers;
using Balance.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Integration.Rabobank;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceIntegrationRabobank(
        this IServiceCollection services
    ) =>
        services
            .AddScoped<IRabobankAccountStatementParser, RabobankAccountStatementParser>()
            .AddScoped<IRabobankCreditCardStatementParser, RabobankCreditCardStatementParser>()
            .AddScoped<IBankTransactionExtractor, RabobankCurrentAccountTransactionExtractor>()
            .AddScoped<IBankTransactionExtractor, RabobankSavingsAccountTransactionExtractor>()
            .AddScoped<IBankTransactionExtractor, RabobankCreditCardTransactionExtractor>();
}
