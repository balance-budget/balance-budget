using Balance.Services.Contracts;
using Balance.Web.EndpointResults;

namespace Balance.Web.Endpoints;

internal static class HtmxEndpoints
{
    public const string PathPrefix = "/htmx";

    public static void MapHtmx(this IEndpointRouteBuilder app) =>
        app.MapGroup(PathPrefix).ExcludeFromDescription().MapStats();

    private static void MapStats(this RouteGroupBuilder group) =>
        group.MapGet(
            "/stats",
            async (
                IApplicationVersionService versionService,
                CancellationToken cancellationToken
            ) =>
            {
                var version = versionService.Version.Split('+').FirstOrDefault();

                return new HtmlResult(
                    $"""
                    <p class="stats">Version: {version}</p>
                    """
                );
            }
        );
}
