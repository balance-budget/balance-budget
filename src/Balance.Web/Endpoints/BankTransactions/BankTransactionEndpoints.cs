using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Services.Contracts;
using FluentValidation;
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
        group.MapPost("", CreateAsync).WithName("CreateBankTransaction");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteBankTransaction");
    }

    private static async Task<Ok<IReadOnlyList<BankTransactionResponse>>> ListAsync(
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var bankTransactions = await bankTransactionService.ListAsync(cancellationToken);
        IReadOnlyList<BankTransactionResponse> responses =
        [
            .. bankTransactions.Select(BankTransactionResponse.From),
        ];
        return TypedResults.Ok(responses);
    }

    private static async Task<Results<Ok<BankTransactionResponse>, NotFound>> GetAsync(
        [FromRoute] Guid id,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var bankTransaction = await bankTransactionService.GetAsync(
            new BankTransactionId(id),
            cancellationToken
        );
        return bankTransaction is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(BankTransactionResponse.From(bankTransaction));
    }

    private static async Task<Created<BankTransactionResponse>> CreateAsync(
        [FromBody] CreateBankTransactionRequest request,
        [FromServices] IBankTransactionService bankTransactionService,
        [FromServices] IValidator<CreateBankTransactionRequest> validator,
        CancellationToken cancellationToken
    )
    {
        await ValidateAsync(validator, request, cancellationToken);

        var bankTransaction = await bankTransactionService.CreateAsync(
            new CreateBankTransactionInput(
                request.BankAccountId,
                request.BookingDate,
                request.Amount,
                request.CurrencyCode
            ),
            cancellationToken
        );
        var response = BankTransactionResponse.From(bankTransaction);
        return TypedResults.Created($"{PathPrefix}/{bankTransaction.Id.Value}", response);
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

    private static async Task ValidateAsync<T>(
        IValidator<T> validator,
        T request,
        CancellationToken cancellationToken
    )
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new DomainException(
                DomainExceptionKind.Validation,
                string.Join("; ", result.Errors.Select(e => e.ErrorMessage)),
                result
                    .Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }
    }
}
