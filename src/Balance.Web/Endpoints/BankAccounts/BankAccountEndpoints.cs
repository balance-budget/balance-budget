using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Services.Contracts;
using FluentValidation;
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
        group.MapGet("/{id:guid}", GetAsync).WithName("GetBankAccount");
        group.MapPost("", CreateAsync).WithName("CreateBankAccount");
        group.MapPatch("/{id:guid}", UpdateAsync).WithName("UpdateBankAccount");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteBankAccount");
    }

    private static async Task<Ok<IReadOnlyList<BankAccountResponse>>> ListAsync(
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        var bankAccounts = await bankAccountService.ListAsync(cancellationToken);
        IReadOnlyList<BankAccountResponse> responses =
        [
            .. bankAccounts.Select(BankAccountResponse.From),
        ];
        return TypedResults.Ok(responses);
    }

    private static async Task<Results<Ok<BankAccountResponse>, NotFound>> GetAsync(
        [FromRoute] Guid id,
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        var bankAccount = await bankAccountService.GetAsync(
            new BankAccountId(id),
            cancellationToken
        );
        return bankAccount is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(BankAccountResponse.From(bankAccount));
    }

    private static async Task<Created<BankAccountResponse>> CreateAsync(
        [FromBody] CreateBankAccountRequest request,
        [FromServices] IBankAccountService bankAccountService,
        [FromServices] IValidator<CreateBankAccountRequest> validator,
        CancellationToken cancellationToken
    )
    {
        await ValidateAsync(validator, request, cancellationToken);

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
        var response = BankAccountResponse.From(bankAccount);
        return TypedResults.Created($"{PathPrefix}/{bankAccount.Id.Value}", response);
    }

    private static async Task<Ok<BankAccountResponse>> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateBankAccountRequest request,
        [FromServices] IBankAccountService bankAccountService,
        [FromServices] IValidator<UpdateBankAccountRequest> validator,
        CancellationToken cancellationToken
    )
    {
        await ValidateAsync(validator, request, cancellationToken);

        var bankAccount = await bankAccountService.UpdateAsync(
            new BankAccountId(id),
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
        return TypedResults.Ok(BankAccountResponse.From(bankAccount));
    }

    private static async Task<NoContent> DeleteAsync(
        [FromRoute] Guid id,
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        await bankAccountService.DeleteAsync(new BankAccountId(id), cancellationToken);
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
