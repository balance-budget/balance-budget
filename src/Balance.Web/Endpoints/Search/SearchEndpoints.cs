using Balance.Services.Contracts;
using Balance.Web.Filters;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Search;

internal static class SearchEndpoints
{
    public const string PathPrefix = "/search";

    public static void MapSearch(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Search");
        group.MapGet("", SearchAsync).WithValidation<SearchRequest>().WithName("Search");
    }

    private static async Task<Ok<SearchOutput>> SearchAsync(
        [AsParameters] SearchRequest request,
        [FromServices] ISearchService searchService,
        CancellationToken cancellationToken
    )
    {
        var output = await searchService.SearchAsync(request.Q, cancellationToken);
        return TypedResults.Ok(output);
    }
}
