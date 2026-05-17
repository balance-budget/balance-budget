using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
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
        group.MapGet("/{id:guid}", GetAsync).WithName("GetBankTransaction");
        group
            .MapPost("", CreateAsync)
            .WithValidation<CreateBankTransactionRequest>()
            .WithName("CreateBankTransaction");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteBankTransaction");
    }

    private static async Task<Ok<IReadOnlyList<BankTransactionOutput>>> ListAsync(
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var bankTransactions = await bankTransactionService.ListAsync(cancellationToken);
        return TypedResults.Ok(bankTransactions);
    }

    private static async Task<Results<Ok<BankTransactionOutput>, NotFound>> GetAsync(
        [FromRoute] Guid id,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var bankTransaction = await bankTransactionService.GetAsync(
            new BankTransactionId(id),
            cancellationToken
        );
        return bankTransaction is null ? TypedResults.NotFound() : TypedResults.Ok(bankTransaction);
    }

    private static async Task<Created<BankTransactionOutput>> CreateAsync(
        [FromBody] CreateBankTransactionRequest request,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var bankTransaction = await bankTransactionService.CreateAsync(
            new CreateBankTransactionInput(
                request.BankAccountId,
                request.BookingDate,
                request.Amount,
                request.CurrencyCode
            ),
            cancellationToken
        );
        return TypedResults.Created($"{PathPrefix}/{bankTransaction.Id.Value}", bankTransaction);
    }

    private static async Task<NoContent> DeleteAsync(
        [FromRoute] Guid id,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        await bankTransactionService.DeleteAsync(new BankTransactionId(id), cancellationToken);
        return TypedResults.NoContent();
    }
}
