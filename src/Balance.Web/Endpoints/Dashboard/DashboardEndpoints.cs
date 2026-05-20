using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Dashboard;

internal static class DashboardEndpoints
{
    public const string PathPrefix = "/dashboard";

    // Hardcoded fallback until per-user currency preferences land.
    private static readonly CurrencyCode DefaultCurrency = new("EUR");

    public static void MapDashboard(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Dashboard");
        group.MapGet("/summary", GetSummaryAsync).WithName("GetDashboardSummary");
    }

    private static async Task<Ok<DashboardSummaryOutput>> GetSummaryAsync(
        [FromQuery] string? currency,
        [FromServices] IDashboardSummaryService dashboardSummaryService,
        CancellationToken cancellationToken
    )
    {
        var currencyCode = string.IsNullOrWhiteSpace(currency)
            ? DefaultCurrency
            : new CurrencyCode(currency);
        var summary = await dashboardSummaryService.GetSummaryAsync(
            currencyCode,
            cancellationToken
        );
        return TypedResults.Ok(summary);
    }
}
