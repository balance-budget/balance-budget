using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
using Balance.Web.OpenApi;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
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
            .WithJsonPatchTarget<UpdateBankAccountInput>()
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

    private static async Task<
        Results<Created<BankAccountOutput>, ProblemHttpResult, ValidationProblem>
    > CreateAsync(
        [FromBody] CreateBankAccountRequest request,
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankAccountService.CreateAsync(
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
        return result.ToCreated(value => $"{PathPrefix}/{value.Id.Value}");
    }

    private static async Task<
        Results<Ok<BankAccountOutput>, ProblemHttpResult, ValidationProblem>
    > UpdateAsync(
        [FromRoute] BankAccountId id,
        [FromBody] JsonPatchDocument<UpdateBankAccountInput> patch,
        [FromServices] IBankAccountService bankAccountService,
        [FromServices] IValidator<UpdateBankAccountInput>? validator,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await bankAccountService.GetSnapshotAsync(id, cancellationToken);
        if (snapshot is null)
        {
            return new Result<BankAccountOutput>(
                new NotFoundError("BankAccount", id.Value.ToString())
            ).ToOk();
        }

        var patched = await patch.ApplyAndValidateAsync(snapshot, validator, cancellationToken);
        if (patched.IsFailure)
        {
            return new Result<BankAccountOutput>(patched.Error).ToOk();
        }

        var result = await bankAccountService.UpdateAsync(id, patched.Value, cancellationToken);
        return result.ToOk();
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> DeleteAsync(
        [FromRoute] BankAccountId id,
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankAccountService.DeleteAsync(id, cancellationToken);
        return result.ToNoContent();
    }
}
