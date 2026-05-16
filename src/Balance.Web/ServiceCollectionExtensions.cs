using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;

namespace Balance.Web;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceWeb(this IServiceCollection services)
    {
        services.AddOpenApi();

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.Configure<RouteOptions>(options =>
        {
            options.LowercaseQueryStrings = true;
            options.LowercaseUrls = true;
        });

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Assuming that Balance runs in docker without being publicly exposed directly,
            // we trust any IP as a safe reverse proxy
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            options.ForwardedHeaders = ForwardedHeaders.All;
        });

        services.AddAuthentication().AddCookie(); // Default scheme for browser access

        services.AddAuthorization();

        services.AddAntiforgery();
        services.AddCors(c =>
            c.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader())
        );
        services.AddHealthChecks();

        services.AddValidatorsFromAssemblyContaining<IWebAssemblyMarker>(
            ServiceLifetime.Singleton,
            includeInternalTypes: true
        );

        return services;
    }
}
