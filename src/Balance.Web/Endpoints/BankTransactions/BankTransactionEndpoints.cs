using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.BankTransactions;

internal static class BankTransactionEndpoints
{
    public const string PathPrefix = "/bank-transactions";

    public static void MapBankTransactions(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("BankTransactions");
        group.MapGet("", ListAsync).WithName("ListBankTransactions");
        group.MapGet("/{id}", GetAsync).WithName("GetBankTransaction");
        group
            .MapPost("", CreateAsync)
            .WithValidation<CreateBankTransactionRequest>()
            .WithName("CreateBankTransaction");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteBankTransaction");
    }

    private static async Task<Ok<IReadOnlyList<BankTransactionOutput>>> ListAsync(
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var bankTransactions = await bankTransactionService.ListAsync(cancellationToken);
        return TypedResults.Ok(bankTransactions);
    }

    private static async Task<
        Results<Ok<BankTransactionOutput>, NotFound<ProblemDetails>, ValidationProblem>
    > GetAsync(
        [FromRoute] BankTransactionId id,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankTransactionService.GetAsync(id, cancellationToken);
        return result.ToOkReadOnly();
    }

    private static async Task<
        Results<
            Created<BankTransactionOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > CreateAsync(
        [FromBody] CreateBankTransactionRequest request,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankTransactionService.CreateAsync(
            new CreateBankTransactionInput(
                request.BankAccountId,
                request.BookingDate,
                request.Amount,
                request.CurrencyCode,
                request.Description,
                request.CounterpartyName,
                request.CounterpartyAccountNumber
            ),
            cancellationToken
        );
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
        [FromRoute] BankTransactionId id,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankTransactionService.DeleteAsync(id, cancellationToken);
        return result.ToNoContent();
    }
}
