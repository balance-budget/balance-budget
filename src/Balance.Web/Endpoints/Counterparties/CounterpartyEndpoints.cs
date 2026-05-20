using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
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
        group.MapPatch("/{id}", UpdateAsync).WithName("UpdateCounterparty");
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

    private static async Task<Created<CounterpartyOutput>> CreateAsync(
        [FromBody] CreateCounterpartyRequest request,
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        var counterparty = await counterpartyService.CreateAsync(request.Name, cancellationToken);
        return TypedResults.Created($"{PathPrefix}/{counterparty.Id.Value}", counterparty);
    }

    private static async Task<Results<Ok<CounterpartyOutput>, NotFound>> UpdateAsync(
        [FromRoute] CounterpartyId id,
        [FromBody] JsonPatchDocument<UpdateCounterpartyInput> patch,
        [FromServices] ICounterpartyService counterpartyService,
        [FromServices] IValidator<UpdateCounterpartyInput>? validator,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await counterpartyService.GetSnapshotAsync(id, cancellationToken);
        if (snapshot is null)
            return TypedResults.NotFound();

        var input = await patch.ApplyAndValidateAsync(snapshot, validator, cancellationToken);
        var counterparty = await counterpartyService.UpdateAsync(id, input, cancellationToken);
        return TypedResults.Ok(counterparty);
    }

    private static async Task<NoContent> DeleteAsync(
        [FromRoute] CounterpartyId id,
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        await counterpartyService.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
