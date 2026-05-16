using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Exceptions;
using FluentValidation;
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
        group.MapGet("/{code}", GetAsync).WithName("GetCurrency");
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
        [FromServices] IValidator<GetCurrencyRequest> validator,
        CancellationToken cancellationToken
    )
    {
        var request = new GetCurrencyRequest(new CurrencyCode(code));
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new DomainException(
                DomainExceptionKind.Validation,
                string.Join("; ", result.Errors.Select(e => e.ErrorMessage))
            );
        }

        var currency = await currencyService.GetAsync(request.Code, cancellationToken);
        return currency is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(CurrencyResponse.From(currency));
    }
}
