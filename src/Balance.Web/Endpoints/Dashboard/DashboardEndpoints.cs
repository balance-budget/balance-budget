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

    private const NetWorthRange DefaultNetWorthRange = NetWorthRange.OneYear;

    // Matches what one account row on the dashboard renders; not user-tunable to keep the
    // batched query bounded.
    private const int RegisterPreviewRowsPerAccount = 5;

    // Top categories shown on the spending widget; the rest fold into an "Other" bucket.
    private const int SpendingCategoryCount = 6;

    public static void MapDashboard(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Dashboard");
        group.MapGet("/summary", GetSummaryAsync).WithName("GetDashboardSummary");
        group
            .MapGet("/account-balance-trend", GetAccountBalanceTrendAsync)
            .WithName("GetAccountBalanceTrend");
        group.MapGet("/net-worth-trend", GetNetWorthTrendAsync).WithName("GetNetWorthTrend");
        group
            .MapGet("/spending-by-category", GetSpendingByCategoryAsync)
            .WithName("GetDashboardSpendingByCategory");
        group
            .MapGet("/register-previews", GetRegisterPreviewsAsync)
            .WithName("GetDashboardRegisterPreviews");
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
            Ok<NetWorthTrendOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > GetNetWorthTrendAsync(
        [FromQuery] NetWorthRange? range,
        [FromQuery] CurrencyCode? currency,
        [FromServices] IDashboardService dashboardService,
        CancellationToken cancellationToken
    )
    {
        var result = await dashboardService.GetNetWorthTrendAsync(
            currency ?? DefaultCurrency,
            range ?? DefaultNetWorthRange,
            cancellationToken
        );
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<SpendingByCategoryOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > GetSpendingByCategoryAsync(
        [FromQuery] CurrencyCode? currency,
        [FromServices] IDashboardService dashboardService,
        CancellationToken cancellationToken
    )
    {
        var result = await dashboardService.GetSpendingByCategoryAsync(
            currency ?? DefaultCurrency,
            SpendingCategoryCount,
            cancellationToken
        );
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<DashboardRegisterPreviewOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > GetRegisterPreviewsAsync(
        [FromServices] IDashboardService dashboardService,
        CancellationToken cancellationToken
    )
    {
        var result = await dashboardService.GetRegisterPreviewsAsync(
            RegisterPreviewRowsPerAccount,
            cancellationToken
        );
        return result.ToOk();
    }
}
