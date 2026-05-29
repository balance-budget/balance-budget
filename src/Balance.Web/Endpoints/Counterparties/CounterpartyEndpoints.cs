using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Counterparties;

internal static class CounterpartyEndpoints
{
    public const string PathPrefix = "/counterparties";

    public static void MapCounterparties(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Counterparties");
        group.MapGet("", ListAsync).WithName("ListCounterparties");
        group.MapGet("/{id}", GetAsync).WithName("GetCounterparty");
        group
            .MapGet("/{id}/suggested-accounts", GetSuggestedAccountsAsync)
            .WithName("GetCounterpartySuggestedAccounts");
        group
            .MapPost("", CreateAsync)
            .WithValidation<CreateCounterpartyRequest>()
            .WithName("CreateCounterparty");
        group
            .MapPatchSnapshotted<
                CounterpartyId,
                ICounterpartyService,
                UpdateCounterpartyInput,
                CounterpartyOutput
            >(
                "/{id}",
                (svc, id, ct) => svc.GetSnapshotAsync(id, ct),
                (svc, id, input, ct) => svc.UpdateAsync(id, input, ct)
            )
            .WithName("UpdateCounterparty");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteCounterparty");
    }

    private static async Task<Ok<PagedOutput<CounterpartyOutput>>> ListAsync(
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        var counterparties = await counterpartyService.ListAsync(cancellationToken);
        return TypedResults.Ok(counterparties);
    }

    private static async Task<
        Results<Ok<CounterpartyOutput>, NotFound<ProblemDetails>, ValidationProblem>
    > GetAsync(
        [FromRoute] CounterpartyId id,
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        var result = await counterpartyService.GetAsync(id, cancellationToken);
        return result.ToOkReadOnly();
    }

    private static async Task<
        Results<
            Ok<IReadOnlyList<SuggestedCounterAccountOutput>>,
            NotFound<ProblemDetails>,
            ValidationProblem
        >
    > GetSuggestedAccountsAsync(
        [FromRoute] CounterpartyId id,
        [FromServices] IAccountSuggestionService suggestionService,
        CancellationToken cancellationToken
    )
    {
        var result = await suggestionService.GetSuggestedCounterAccountsAsync(
            id,
            cancellationToken
        );
        return result.ToOkReadOnly();
    }

    private static async Task<
        Results<
            Created<CounterpartyOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > CreateAsync(
        [FromBody] CreateCounterpartyRequest request,
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        var result = await counterpartyService.CreateAsync(request.Name, cancellationToken);
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
        [FromRoute] CounterpartyId id,
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        var result = await counterpartyService.DeleteAsync(id, cancellationToken);
        return result.ToNoContent();
    }
}
