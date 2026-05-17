using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Accounts;

internal static class AccountEndpoints
{
    public const string PathPrefix = "/accounts";

    public static void MapAccounts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("Accounts");
        group.MapGet("", ListAsync).WithName("ListAccounts");
        group.MapGet("/{id}", GetAsync).WithName("GetAccount");
        group.MapGet("/{id}/balance", GetBalanceAsync).WithName("GetAccountBalance");
        group
            .MapPost("", CreateAsync)
            .WithValidation<CreateAccountRequest>()
            .WithName("CreateAccount");
        group
            .MapPatch("/{id}", UpdateAsync)
            .WithJsonPatch<UpdateAccountInput>(LoadAccountSnapshotAsync)
            .WithName("UpdateAccount");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteAccount");
    }

    private static async Task<Ok<IReadOnlyList<AccountOutput>>> ListAsync(
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        var accounts = await accountService.ListAsync(cancellationToken);
        return TypedResults.Ok(accounts);
    }

    private static async Task<Results<Ok<AccountOutput>, NotFound>> GetAsync(
        [FromRoute] AccountId id,
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        var account = await accountService.GetAsync(id, cancellationToken);
        return account is null ? TypedResults.NotFound() : TypedResults.Ok(account);
    }

    private static async Task<Results<Ok<Money>, NotFound>> GetBalanceAsync(
        [FromRoute] AccountId id,
        [FromServices] IAccountBalanceService accountBalanceService,
        CancellationToken cancellationToken
    )
    {
        var balance = await accountBalanceService.GetBalanceAsync(id, cancellationToken);
        return balance is null ? TypedResults.NotFound() : TypedResults.Ok(balance.Value);
    }

    private static async Task<Created<AccountOutput>> CreateAsync(
        [FromBody] CreateAccountRequest request,
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        var account = await accountService.CreateAsync(
            request.Name,
            request.AccountType,
            request.CurrencyCode,
            cancellationToken
        );
        return TypedResults.Created($"{PathPrefix}/{account.Id.Value}", account);
    }

    private static async Task<Ok<AccountOutput>> UpdateAsync(
        [FromRoute] AccountId id,
        [FromBody] JsonPatchDocument<UpdateAccountInput> patch,
        HttpContext httpContext,
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        _ = patch;
        var input = JsonPatchEndpointFilter.GetSnapshot<UpdateAccountInput>(httpContext);
        var account = await accountService.UpdateAsync(id, input, cancellationToken);
        return TypedResults.Ok(account);
    }

    private static async Task<NoContent> DeleteAsync(
        [FromRoute] AccountId id,
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        await accountService.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<UpdateAccountInput?> LoadAccountSnapshotAsync(
        EndpointFilterInvocationContext context,
        CancellationToken cancellationToken
    )
    {
        var id = context.Arguments.OfType<AccountId>().FirstOrDefault();
        var service = context.HttpContext.RequestServices.GetRequiredService<IAccountService>();
        return await service.GetSnapshotAsync(id, cancellationToken);
    }
}
