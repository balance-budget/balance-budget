using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
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
            .MapPatchSnapshotted<AccountId, IAccountService, UpdateAccountInput, AccountOutput>(
                "/{id}",
                (svc, id, ct) => svc.GetSnapshotAsync(id, ct),
                (svc, id, input, ct) => svc.UpdateAsync(id, input, ct)
            )
            .WithName("UpdateAccount");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteAccount");
    }

    private static async Task<Ok<PagedOutput<AccountOutput>>> ListAsync(
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
        Results<Ok<PagedOutput<RegisterRowOutput>>, NotFound<ProblemDetails>, ValidationProblem>
    > ListRegisterAsync(
        [FromRoute] AccountId id,
        [AsParameters] ListAccountRegisterRequest request,
        [FromServices] IRegisterService registerService,
        CancellationToken cancellationToken
    )
    {
        var skip = request.Skip ?? 0;
        var take = request.Take ?? ListAccountRegisterRequest.DefaultPageSize;
        var result = await registerService.ListAsync(id, skip, take, request.Q, cancellationToken);
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
            new CreateAccountInput
            {
                Name = request.Name,
                Code = request.Code,
                AccountType = request.AccountType,
                CurrencyCode = request.CurrencyCode,
                IsPostable = request.IsPostable,
                ParentAccountId = request.ParentAccountId,
                IconName = request.IconName,
            },
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
        [FromRoute] AccountId id,
        [FromServices] IAccountService accountService,
        CancellationToken cancellationToken
    )
    {
        var result = await accountService.DeleteAsync(id, cancellationToken);
        return result.ToNoContent();
    }
}
