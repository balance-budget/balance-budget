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

    private const TrendRange DefaultTrendRange = TrendRange.ThreeMonths;

    public static void MapDashboard(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Dashboard");
        group.MapGet("/summary", GetSummaryAsync).WithName("GetDashboardSummary");
        group
            .MapGet("/account-balance-trend", GetAccountBalanceTrendAsync)
            .WithName("GetAccountBalanceTrend");
    }

    private static async Task<Ok<DashboardSummaryOutput>> GetSummaryAsync(
        [FromQuery] string? currency,
        [FromServices] IDashboardService dashboardService,
        CancellationToken cancellationToken
    )
    {
        var currencyCode = string.IsNullOrWhiteSpace(currency)
            ? DefaultCurrency
            : new CurrencyCode(currency);
        var summary = await dashboardService.GetSummaryAsync(currencyCode, cancellationToken);
        return TypedResults.Ok(summary);
    }

    private static async Task<
        Results<Ok<AccountBalanceTrendOutput>, BadRequest<string>>
    > GetAccountBalanceTrendAsync(
        [FromQuery] string? range,
        [FromQuery] string? currency,
        [FromServices] IDashboardService dashboardService,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseTrendRange(range, out var trendRange))
        {
            return TypedResults.BadRequest(
                $"Invalid 'range' value '{range}'. Expected one of: 1M, 3M, 6M, 1Y."
            );
        }

        var currencyCode = string.IsNullOrWhiteSpace(currency)
            ? DefaultCurrency
            : new CurrencyCode(currency);

        var trend = await dashboardService.GetAccountBalanceTrendAsync(
            currencyCode,
            trendRange,
            cancellationToken
        );
        return TypedResults.Ok(trend);
    }

    private static bool TryParseTrendRange(string? raw, out TrendRange range)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            range = DefaultTrendRange;
            return true;
        }

        switch (raw.ToUpperInvariant())
        {
            case "1M":
                range = TrendRange.OneMonth;
                return true;
            case "3M":
                range = TrendRange.ThreeMonths;
                return true;
            case "6M":
                range = TrendRange.SixMonths;
                return true;
            case "1Y":
                range = TrendRange.OneYear;
                return true;
            default:
                range = default;
                return false;
        }
    }
}
