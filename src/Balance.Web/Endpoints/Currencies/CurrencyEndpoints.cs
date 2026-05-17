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
            .WithValidation(static ctx => new GetCurrencyRequest(
                new CurrencyCode((string)ctx.Arguments[0]!)
            ))
            .WithName("GetCurrency");
    }

    private static async Task<Ok<IReadOnlyList<CurrencyResponse>>> ListAsync(
        [FromServices] ICurrencyService currencyService,
        CancellationToken cancellationToken
    )
    {
        var currencies = await currencyService.ListAsync(cancellationToken);
        IReadOnlyList<CurrencyResponse> responses = [.. currencies.Select(CurrencyResponse.From)];
        return TypedResults.Ok(responses);
    }

    private static async Task<Results<Ok<CurrencyResponse>, NotFound>> GetAsync(
        [FromRoute] string code,
        [FromServices] ICurrencyService currencyService,
        CancellationToken cancellationToken
    )
    {
        var currency = await currencyService.GetAsync(new CurrencyCode(code), cancellationToken);
        return currency is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(CurrencyResponse.From(currency));
    }
}
