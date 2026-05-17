using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
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
            .WithJsonPatch<UpdateCounterpartyInput>(LoadCounterpartySnapshotAsync)
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
        [FromRoute] CounterpartyId id,
        [FromBody] JsonPatchDocument<UpdateCounterpartyInput> patch,
        HttpContext httpContext,
        [FromServices] ICounterpartyService counterpartyService,
        CancellationToken cancellationToken
    )
    {
        _ = patch;
        var input = JsonPatchEndpointFilter.GetSnapshot<UpdateCounterpartyInput>(httpContext);
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

    private static async Task<UpdateCounterpartyInput?> LoadCounterpartySnapshotAsync(
        EndpointFilterInvocationContext context,
        CancellationToken cancellationToken
    )
    {
        var id = context.Arguments.OfType<CounterpartyId>().FirstOrDefault();
        var service =
            context.HttpContext.RequestServices.GetRequiredService<ICounterpartyService>();
        return await service.GetSnapshotAsync(id, cancellationToken);
    }
}
