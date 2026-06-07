using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Loans;

internal static class LoanEndpoints
{
    public const string PathPrefix = "/loans";

    public static void MapLoans(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Loans");
        group.MapGet("", ListAsync).WithName("ListLoans");
        group.MapGet("/{id}", GetAsync).WithName("GetLoan");
        group.MapPost("", CreateAsync).WithValidation<CreateLoanRequest>().WithName("CreateLoan");
        group
            .MapPatchSnapshotted<LoanId, ILoanService, UpdateLoanInput, LoanDetailOutput>(
                "/{id}",
                (svc, id, ct) => svc.GetSnapshotAsync(id, ct),
                (svc, id, input, ct) => svc.UpdateAsync(id, input, ct)
            )
            .WithName("UpdateLoan");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteLoan");
        group
            .MapPost("/{id}/parts", AddPartAsync)
            .WithValidation<CreateLoanPartRequest>()
            .WithName("AddLoanPart");
        group
            .MapPost("/{id}/parts/{partId}/rate-periods", AddRatePeriodAsync)
            .WithValidation<LoanRatePeriodRequest>()
            .WithName("AddLoanPartRatePeriod");
    }

    private static async Task<Ok<IReadOnlyList<LoanOutput>>> ListAsync(
        [FromServices] ILoanService loanService,
        CancellationToken cancellationToken
    )
    {
        var loans = await loanService.ListAsync(cancellationToken);
        return TypedResults.Ok(loans);
    }

    private static async Task<
        Results<Ok<LoanDetailOutput>, NotFound<ProblemDetails>, ValidationProblem>
    > GetAsync(
        [FromRoute] LoanId id,
        [FromServices] ILoanService loanService,
        CancellationToken cancellationToken
    )
    {
        var result = await loanService.GetAsync(id, cancellationToken);
        return result.ToOkReadOnly();
    }

    private static async Task<
        Results<
            Created<LoanDetailOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > CreateAsync(
        [FromBody] CreateLoanRequest request,
        [FromServices] ILoanService loanService,
        CancellationToken cancellationToken
    )
    {
        var result = await loanService.CreateAsync(request.ToInput(), cancellationToken);
        return result.ToCreatedAt(PathPrefix, v => v.Id.Value);
    }

    private static async Task<
        Results<
            NoContent,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > DeleteAsync(
        [FromRoute] LoanId id,
        [FromServices] ILoanService loanService,
        CancellationToken cancellationToken
    )
    {
        var result = await loanService.DeleteAsync(id, cancellationToken);
        return result.ToNoContent();
    }

    private static async Task<
        Results<
            Ok<LoanDetailOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > AddPartAsync(
        [FromRoute] LoanId id,
        [FromBody] CreateLoanPartRequest request,
        [FromServices] ILoanService loanService,
        CancellationToken cancellationToken
    )
    {
        var result = await loanService.AddPartAsync(id, request.ToInput(), cancellationToken);
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<LoanDetailOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > AddRatePeriodAsync(
        [FromRoute] LoanId id,
        [FromRoute] LoanPartId partId,
        [FromBody] LoanRatePeriodRequest request,
        [FromServices] ILoanService loanService,
        CancellationToken cancellationToken
    )
    {
        var result = await loanService.AddRatePeriodAsync(
            id,
            partId,
            new CreateLoanRatePeriodInput(
                request.EffectiveDate,
                request.AnnualRatePercent,
                request.FixedUntil
            ),
            cancellationToken
        );
        return result.ToOk();
    }
}
