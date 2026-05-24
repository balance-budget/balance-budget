using System.Diagnostics;
using System.Text.Json.Serialization;
using Balance.Configuration.Helpers;
using Balance.Configuration.Options;
using Balance.Data;
using Balance.Web.OpenApi;
using FluentValidation;
using Microsoft.AspNetCore.Http.Features;
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
            options.AddOperationTransformer<JsonPatchOperationTransformer>();
            options.AddSchemaTransformer<TypedIdSchemaTransformer>();
            // Final sweep over components.schemas — backstops typed-IDs that the
            // per-schema pass missed (see remarks on the transformer).
            options.AddDocumentTransformer<TypedIdSchemaTransformer>();
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

        // 5 MB cap covers ING current-account CSVs (kilobytes in practice) with headroom; the
        // only multipart endpoint today is POST /api/bank-accounts/{id}/imports.
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 5L * 1024 * 1024;
        });

        var reverseProxyOptions = configuration.GetSection<ReverseProxyOptions>();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Trust only the networks listed in ReverseProxy:KnownNetworks. The framework
            // defaults (loopback) are cleared so the configured list is authoritative; if it
            // is empty no proxies are trusted and X-Forwarded-* headers are ignored.
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
