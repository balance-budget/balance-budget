using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Reports;

internal static class ReportEndpoints
{
    public const string PathPrefix = "/reports";

    // Hardcoded fallback until per-user currency preferences land (mirrors DashboardEndpoints).
    private static readonly CurrencyCode DefaultCurrency = new("EUR");

    public static void MapReports(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Reports");
        group.MapGet("/distribution", GetDistributionAsync).WithName("GetDistribution");
        group.MapGet("/flow", GetMoneyFlowAsync).WithName("GetMoneyFlow");
    }

    private static async Task<
        Results<
            Ok<DistributionOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > GetDistributionAsync(
        [FromQuery] DistributionType type,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] CurrencyCode? currency,
        [FromQuery] AccountId? parentId,
        [FromServices] IReportsService reportsService,
        CancellationToken cancellationToken
    )
    {
        var result = await reportsService.GetDistributionAsync(
            type,
            parentId,
            from,
            to,
            currency ?? DefaultCurrency,
            cancellationToken
        );
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<MoneyFlowOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > GetMoneyFlowAsync(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] CurrencyCode? currency,
        [FromQuery] AccountId[]? expanded,
        [FromServices] IReportsService reportsService,
        CancellationToken cancellationToken
    )
    {
        var result = await reportsService.GetMoneyFlowAsync(
            from,
            to,
            currency ?? DefaultCurrency,
            // Empty set draws roots only; each id opts that node into showing its children.
            (expanded ?? []).ToHashSet(),
            cancellationToken
        );
        return result.ToOk();
    }
}
