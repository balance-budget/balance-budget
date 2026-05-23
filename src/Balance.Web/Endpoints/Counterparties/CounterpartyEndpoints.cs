using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
using Balance.Web.OpenApi;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
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
            .MapPost("", CreateAsync)
            .WithValidation<CreateCounterpartyRequest>()
            .WithName("CreateCounterparty");
        group
            .MapPatch("/{id}", UpdateAsync)
            .WithJsonPatchTarget<UpdateCounterpartyInput>()
            .WithName("UpdateCounterparty");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteCounterparty");
    }

    private static async Task<Ok<IReadOnlyList<CounterpartyOutput>>> ListAsync(
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        var counterparties = await counterpartyService.ListAsync(cancellationToken);
        return TypedResults.Ok(counterparties);
    }

    private static async Task<Results<Ok<CounterpartyOutput>, NotFound>> GetAsync(
        [FromRoute] CounterpartyId id,
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        var counterparty = await counterpartyService.GetAsync(id, cancellationToken);
        return counterparty is null ? TypedResults.NotFound() : TypedResults.Ok(counterparty);
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
        return result.ToCreated(value => $"{PathPrefix}/{value.Id.Value}");
    }

    private static async Task<
        Results<
            Ok<CounterpartyOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > UpdateAsync(
        [FromRoute] CounterpartyId id,
        [FromBody] JsonPatchDocument<UpdateCounterpartyInput> patch,
        [FromServices] ICounterpartyService counterpartyService,
        [FromServices] IValidator<UpdateCounterpartyInput>? validator,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await counterpartyService.GetSnapshotAsync(id, cancellationToken);
        if (snapshot is null)
        {
            return new Result<CounterpartyOutput>(
                new NotFoundError("Counterparty", id.Value.ToString())
            ).ToOk();
        }

        var patched = await patch.ApplyAndValidateAsync(snapshot, validator, cancellationToken);
        if (patched.IsFailure)
        {
            return new Result<CounterpartyOutput>(patched.Error).ToOk();
        }

        var result = await counterpartyService.UpdateAsync(id, patched.Value, cancellationToken);
        return result.ToOk();
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
