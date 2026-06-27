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
            .AddScoped<IIngSavingsAccountStatementParser, IngSavingsAccountStatementParser>()
            .AddScoped<
                IIngCreditCardStatementParser,
                IngLegacyCreditCardStatementParser
            >()
            .AddScoped<
                IIngCreditCardStatementParser,
                IngModernCreditCardStatementParser
            >()
            .AddScoped<IBankTransactionExtractor, IngBankTransactionExtractor>()
            .AddScoped<IBankTransactionExtractor, IngSavingsAccountTransactionExtractor>()
            .AddScoped<IBankTransactionExtractor, IngCreditCardTransactionExtractor>();
}
