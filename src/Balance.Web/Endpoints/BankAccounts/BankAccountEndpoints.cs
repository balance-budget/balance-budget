using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.BankAccounts;

internal static class BankAccountEndpoints
{
    public const string PathPrefix = "/bank-accounts";

    public static void MapBankAccounts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("BankAccounts");
        group.MapGet("", ListAsync).WithName("ListBankAccounts");
        group.MapGet("/{id}", GetAsync).WithName("GetBankAccount");
        group
            .MapPost("", CreateAsync)
            .WithValidation<CreateBankAccountRequest>()
            .WithName("CreateBankAccount");
        group
            .MapPatch("/{id}", UpdateAsync)
            .WithValidation<UpdateBankAccountRequest>()
            .WithName("UpdateBankAccount");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteBankAccount");
    }

    private static async Task<Ok<IReadOnlyList<BankAccountOutput>>> ListAsync(
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        var bankAccounts = await bankAccountService.ListAsync(cancellationToken);
        return TypedResults.Ok(bankAccounts);
    }

    private static async Task<Results<Ok<BankAccountOutput>, NotFound>> GetAsync(
        [FromRoute] BankAccountId id,
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        var bankAccount = await bankAccountService.GetAsync(id, cancellationToken);
        return bankAccount is null ? TypedResults.NotFound() : TypedResults.Ok(bankAccount);
    }

    private static async Task<Created<BankAccountOutput>> CreateAsync(
        [FromBody] CreateBankAccountRequest request,
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        var bankAccount = await bankAccountService.CreateAsync(
            new CreateBankAccountInput(
                request.Iban,
                request.AccountNumber,
                request.Bic,
                request.BankName,
                request.AccountHolderName,
                request.CurrencyCode,
                request.AccountId,
                request.CounterpartyId
            ),
            cancellationToken
        );
        return TypedResults.Created($"{PathPrefix}/{bankAccount.Id.Value}", bankAccount);
    }

    private static async Task<Ok<BankAccountOutput>> UpdateAsync(
        [FromRoute] BankAccountId id,
        [FromBody] UpdateBankAccountRequest request,
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        var bankAccount = await bankAccountService.UpdateAsync(
            id,
            new UpdateBankAccountInput(
                request.Iban,
                request.AccountNumber,
                request.Bic,
                request.BankName,
                request.AccountHolderName,
                request.CurrencyCode,
                request.AccountId,
                request.CounterpartyId
            ),
            cancellationToken
        );
        return TypedResults.Ok(bankAccount);
    }

    private static async Task<NoContent> DeleteAsync(
        [FromRoute] BankAccountId id,
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        await bankAccountService.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
