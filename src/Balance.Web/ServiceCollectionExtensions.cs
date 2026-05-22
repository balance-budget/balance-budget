using System.Diagnostics;
using System.Text.Json.Serialization;
using Balance.Configuration.Helpers;
using Balance.Configuration.Options;
using Balance.Data;
using Balance.Web.OpenApi;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace Balance.Web;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceWeb(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOpenApi(options =>
        {
            options.AddOperationTransformer<ProblemDetailsOperationTransformer>();
            options.AddOperationTransformer<JsonPatchOperationTransformer>();
            options.AddSchemaTransformer<TypedIdSchemaTransformer>();
        });

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??=
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                if (context.ProblemDetails.Status is { } status)
                {
                    context.ProblemDetails.Type ??= $"https://httpstatuses.com/{status}";
                }
            };
        });

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.Configure<RouteOptions>(options =>
        {
            options.LowercaseQueryStrings = true;
            options.LowercaseUrls = true;
        });

        var reverseProxyOptions = configuration.GetSection<ReverseProxyOptions>();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Assuming that Balance runs in docker without being publicly exposed directly,
            // we trust any IP as a safe reverse proxy
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            options.ForwardedHeaders = ForwardedHeaders.All;

            foreach (var network in reverseProxyOptions.KnownNetworks)
            {
                options.KnownIPNetworks.Add(IPNetwork.Parse(network));
            }
        });

        services.AddAuthentication().AddCookie(); // Default scheme for browser access

        services.AddAuthorization();

        services.AddAntiforgery();
        services.AddCors();
        services.AddHealthChecks().AddDbContextCheck<BalanceDbContext>(tags: ["readiness"]);
        services.AddValidatorsFromAssemblyContaining<IWebAssemblyMarker>(
            includeInternalTypes: true
        );

        return services;
    }
}
