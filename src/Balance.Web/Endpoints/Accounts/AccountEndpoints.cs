using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
using Balance.Web.OpenApi;
using FluentValidation;
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
            .MapGet("/{id}/register", ListRegisterAsync)
            .WithValidation<ListAccountRegisterRequest>()
            .WithName("ListAccountRegister");
        group
            .MapPost("", CreateAsync)
            .WithValidation<CreateAccountRequest>()
            .WithName("CreateAccount");
        group
            .MapPatch("/{id}", UpdateAsync)
            .WithJsonPatchTarget<UpdateAccountInput>()
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

    private static async Task<
        Results<Ok<AccountOutput>, NotFound<ProblemDetails>, ValidationProblem>
    > GetAsync(
        [FromRoute] AccountId id,
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        var result = await accountService.GetAsync(id, cancellationToken);
        return result.ToOkReadOnly();
    }

    private static async Task<
        Results<Ok<IReadOnlyList<RegisterRowOutput>>, NotFound<ProblemDetails>, ValidationProblem>
    > ListRegisterAsync(
        [FromRoute] AccountId id,
        [AsParameters] ListAccountRegisterRequest request,
        [FromServices] IRegisterService registerService,
        CancellationToken cancellationToken
    )
    {
        var skip = request.Skip ?? 0;
        var take = request.Take ?? ListAccountRegisterRequest.DefaultPageSize;
        var result = await registerService.ListAsync(id, skip, take, cancellationToken);
        return result.ToOkReadOnly();
    }

    private static async Task<
        Results<Ok<Money>, NotFound<ProblemDetails>, ValidationProblem>
    > GetBalanceAsync(
        [FromRoute] AccountId id,
        [FromServices] IAccountBalanceService accountBalanceService,
        CancellationToken cancellationToken
    )
    {
        var result = await accountBalanceService.GetBalanceAsync(id, cancellationToken);
        return result.ToOkReadOnly();
    }

    private static async Task<
        Results<
            Created<AccountOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > CreateAsync(
        [FromBody] CreateAccountRequest request,
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        var result = await accountService.CreateAsync(
            request.Name,
            request.AccountType,
            request.CurrencyCode,
            cancellationToken
        );
        return result.ToCreated(value => $"{PathPrefix}/{value.Id.Value}");
    }

    private static async Task<
        Results<
            Ok<AccountOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > UpdateAsync(
        [FromRoute] AccountId id,
        [FromBody] JsonPatchDocument<UpdateAccountInput> patch,
        [FromServices] IAccountService accountService,
        [FromServices] IValidator<UpdateAccountInput>? validator,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await accountService.GetSnapshotAsync(id, cancellationToken);
        if (snapshot.IsFailure)
        {
            return new Result<AccountOutput>(snapshot.Error).ToOk();
        }

        var patched = await patch.ApplyAndValidateAsync(
            snapshot.Value,
            validator,
            cancellationToken
        );
        if (patched.IsFailure)
        {
            return new Result<AccountOutput>(patched.Error).ToOk();
        }

        var result = await accountService.UpdateAsync(id, patched.Value, cancellationToken);
        return result.ToOk();
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
        [FromRoute] AccountId id,
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        var result = await accountService.DeleteAsync(id, cancellationToken);
        return result.ToNoContent();
    }
}
