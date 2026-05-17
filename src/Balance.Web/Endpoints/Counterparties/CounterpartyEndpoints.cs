using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
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
        group.MapGet("/{id:guid}", GetAsync).WithName("GetCounterparty");
        group
            .MapPost("", CreateAsync)
            .WithValidation<CreateCounterpartyRequest>()
            .WithName("CreateCounterparty");
        group
            .MapPatch("/{id:guid}", UpdateAsync)
            .WithValidation<UpdateCounterpartyRequest>()
            .WithName("UpdateCounterparty");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteCounterparty");
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
        [FromRoute] Guid id,
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        var counterparty = await counterpartyService.GetAsync(
            new CounterpartyId(id),
            cancellationToken
        );
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

    private static async Task<Ok<CounterpartyOutput>> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateCounterpartyRequest request,
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        var counterparty = await counterpartyService.UpdateAsync(
            new CounterpartyId(id),
            request.Name,
            cancellationToken
        );
        return TypedResults.Ok(counterparty);
    }

    private static async Task<NoContent> DeleteAsync(
        [FromRoute] Guid id,
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        await counterpartyService.DeleteAsync(new CounterpartyId(id), cancellationToken);
        return TypedResults.NoContent();
    }
}
