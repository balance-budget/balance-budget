using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Currencies;

internal static class CurrencyEndpoints
{
    public const string PathPrefix = "/currencies";

    public static void MapCurrencies(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Currencies");
        group.MapGet("", ListAsync).WithName("ListCurrencies");
        group
            .MapGet("/{code}", GetAsync)
            .WithValidation(static ctx => new GetCurrencyRequest((CurrencyCode)ctx.Arguments[0]!))
            .WithName("GetCurrency");
        group
            .MapPost("", CreateAsync)
            .WithValidation<CreateCurrencyRequest>()
            .WithName("CreateCurrency");
        group
            .MapPatch("/{code}", UpdateAsync)
            .WithValidation<UpdateCurrencyRequest>()
            .WithName("UpdateCurrency");
        group.MapDelete("/{code}", DeleteAsync).WithName("DeleteCurrency");
    }

    private static async Task<Ok<IReadOnlyList<CurrencyOutput>>> ListAsync(
        [FromServices] ICurrencyService currencyService,
        CancellationToken cancellationToken
    )
    {
        var currencies = await currencyService.ListAsync(cancellationToken);
        return TypedResults.Ok(currencies);
    }

    private static async Task<Results<Ok<CurrencyOutput>, NotFound>> GetAsync(
        [FromRoute] CurrencyCode code,
        [FromServices] ICurrencyService currencyService,
        CancellationToken cancellationToken
    )
    {
        var currency = await currencyService.GetAsync(code, cancellationToken);
        return currency is null ? TypedResults.NotFound() : TypedResults.Ok(currency);
    }

    private static async Task<Created<CurrencyOutput>> CreateAsync(
        [FromBody] CreateCurrencyRequest request,
        [FromServices] ICurrencyService currencyService,
        CancellationToken cancellationToken
    )
    {
        var output = await currencyService.CreateAsync(
            new CreateCurrencyInput(
                request.Code,
                request.Name,
                request.MinorUnitScale,
                request.Symbol
            ),
            cancellationToken
        );
        return TypedResults.Created($"{PathPrefix}/{output.Code.Value}", output);
    }

    private static async Task<Ok<CurrencyOutput>> UpdateAsync(
        [FromRoute] CurrencyCode code,
        [FromBody] UpdateCurrencyRequest request,
        [FromServices] ICurrencyService currencyService,
        CancellationToken cancellationToken
    )
    {
        var output = await currencyService.UpdateAsync(
            code,
            new UpdateCurrencyInput(request.Name, request.Symbol),
            cancellationToken
        );
        return TypedResults.Ok(output);
    }

    private static async Task<NoContent> DeleteAsync(
        [FromRoute] CurrencyCode code,
        [FromServices] ICurrencyService currencyService,
        CancellationToken cancellationToken
    )
    {
        await currencyService.DeleteAsync(code, cancellationToken);
        return TypedResults.NoContent();
    }
}
