using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Dashboard;

internal static class DashboardEndpoints
{
    public const string PathPrefix = "/dashboard";

    // Hardcoded fallback until per-user currency preferences land.
    private static readonly CurrencyCode DefaultCurrency = new("EUR");

    private const TrendRange DefaultTrendRange = TrendRange.ThreeMonths;

    // Matches what one account row on the dashboard renders; not user-tunable to keep the
    // batched query bounded.
    private const int RecentActivityRowsPerAccount = 5;

    public static void MapDashboard(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Dashboard");
        group.MapGet("/summary", GetSummaryAsync).WithName("GetDashboardSummary");
        group
            .MapGet("/account-balance-trend", GetAccountBalanceTrendAsync)
            .WithName("GetAccountBalanceTrend");
        group
            .MapGet("/recent-activity", GetRecentActivityAsync)
            .WithName("GetDashboardRecentActivity");
    }

    private static async Task<
        Results<
            Ok<DashboardSummaryOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > GetSummaryAsync(
        [FromQuery] CurrencyCode? currency,
        [FromServices] IDashboardService dashboardService,
        CancellationToken cancellationToken
    )
    {
        var result = await dashboardService.GetSummaryAsync(
            currency ?? DefaultCurrency,
            cancellationToken
        );
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<AccountBalanceTrendOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > GetAccountBalanceTrendAsync(
        [FromQuery] TrendRange? range,
        [FromQuery] CurrencyCode? currency,
        [FromServices] IDashboardService dashboardService,
        CancellationToken cancellationToken
    )
    {
        var result = await dashboardService.GetAccountBalanceTrendAsync(
            currency ?? DefaultCurrency,
            range ?? DefaultTrendRange,
            cancellationToken
        );
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<DashboardRecentActivityOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > GetRecentActivityAsync(
        [FromServices] IDashboardService dashboardService,
        CancellationToken cancellationToken
    )
    {
        var result = await dashboardService.GetRecentActivityAsync(
            RecentActivityRowsPerAccount,
            cancellationToken
        );
        return result.ToOk();
    }
}
