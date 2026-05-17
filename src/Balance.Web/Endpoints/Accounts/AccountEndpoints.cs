using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Services.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Accounts;

internal static class AccountEndpoints
{
    public const string PathPrefix = "/accounts";

    public static void MapAccounts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Accounts");
        group.MapGet("", ListAsync).WithName("ListAccounts");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetAccount");
        group.MapGet("/{id:guid}/balance", GetBalanceAsync).WithName("GetAccountBalance");
        group.MapPost("", CreateAsync).WithName("CreateAccount");
        group.MapPatch("/{id:guid}", UpdateAsync).WithName("UpdateAccount");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteAccount");
    }

    private static async Task<Ok<IReadOnlyList<AccountResponse>>> ListAsync(
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        var accounts = await accountService.ListAsync(cancellationToken);
        IReadOnlyList<AccountResponse> responses = [.. accounts.Select(AccountResponse.From)];
        return TypedResults.Ok(responses);
    }

    private static async Task<Results<Ok<AccountResponse>, NotFound>> GetAsync(
        [FromRoute] Guid id,
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        var account = await accountService.GetAsync(new AccountId(id), cancellationToken);
        return account is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(AccountResponse.From(account));
    }

    private static async Task<Results<Ok<Money>, NotFound>> GetBalanceAsync(
        [FromRoute] Guid id,
        [FromServices] IAccountBalanceService accountBalanceService,
        CancellationToken cancellationToken
    )
    {
        var balance = await accountBalanceService.GetBalanceAsync(
            new AccountId(id),
            cancellationToken
        );
        return balance is null ? TypedResults.NotFound() : TypedResults.Ok(balance.Value);
    }

    private static async Task<Created<AccountResponse>> CreateAsync(
        [FromBody] CreateAccountRequest request,
        [FromServices] IAccountService accountService,
        [FromServices] IValidator<CreateAccountRequest> validator,
        CancellationToken cancellationToken
    )
    {
        await ValidateAsync(validator, request, cancellationToken);

        var account = await accountService.CreateAsync(
            request.Name,
            request.AccountType,
            request.CurrencyCode,
            cancellationToken
        );
        var response = AccountResponse.From(account);
        return TypedResults.Created($"{PathPrefix}/{account.Id.Value}", response);
    }

    private static async Task<Ok<AccountResponse>> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateAccountRequest request,
        [FromServices] IAccountService accountService,
        [FromServices] IValidator<UpdateAccountRequest> validator,
        CancellationToken cancellationToken
    )
    {
        await ValidateAsync(validator, request, cancellationToken);

        var account = await accountService.UpdateAsync(
            new AccountId(id),
            request.Name,
            request.AccountType,
            request.CurrencyCode,
            cancellationToken
        );
        return TypedResults.Ok(AccountResponse.From(account));
    }

    private static async Task<NoContent> DeleteAsync(
        [FromRoute] Guid id,
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        await accountService.DeleteAsync(new AccountId(id), cancellationToken);
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
                string.Join("; ", result.Errors.Select(e => e.ErrorMessage))
            );
        }
    }
}
