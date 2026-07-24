using Balance.Integration.Stater.Contracts;
using Balance.Integration.Stater.Importers;
using Balance.Integration.Stater.Parsers;
using Balance.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Integration.Stater;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceIntegrationStater(
        this IServiceCollection services
    ) =>
        services
            .AddScoped<IStaterStatementParser, StaterStatementParser>()
            .AddScoped<IBankTransactionExtractor, StaterConstructionDepositExtractor>();
}
