using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
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
            .MapPatchSnapshotted<
                BankAccountId,
                IBankAccountService,
                UpdateBankAccountInput,
                BankAccountOutput
            >(
                "/{id}",
                (svc, id, ct) => svc.GetSnapshotAsync(id, ct),
                (svc, id, input, ct) => svc.UpdateAsync(id, input, ct)
            )
            .WithName("UpdateBankAccount");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteBankAccount");
        // Multipart upload — antiforgery is auto-required for IFormFile bindings; we opt out
        // because the v1 auth/antiforgery posture is dormant (matches JSON POST endpoints).
        group
            .MapPost("/{id}/imports", ImportStatementAsync)
            .DisableAntiforgery()
            .WithName("ImportBankAccountStatement");
    }

    private static async Task<Ok<IReadOnlyList<BankAccountOutput>>> ListAsync(
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        var bankAccounts = await bankAccountService.ListAsync(cancellationToken);
        return TypedResults.Ok(bankAccounts);
    }

    private static async Task<
        Results<Ok<BankAccountOutput>, NotFound<ProblemDetails>, ValidationProblem>
    > GetAsync(
        [FromRoute] BankAccountId id,
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankAccountService.GetAsync(id, cancellationToken);
        return result.ToOkReadOnly();
    }

    private static async Task<
        Results<
            Created<BankAccountOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
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
        return result.ToCreatedAt(PathPrefix, v => v.Id.Value);
    }

    private static async Task<
        Results<
            Ok<ImportResult>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > ImportStatementAsync(
        [FromRoute] BankAccountId id,
        IFormFile file,
        [FromServices] IBankTransactionImportService importService,
        CancellationToken cancellationToken
    )
    {
        if (file.Length == 0)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["file"] = ["File must not be empty."] }
            );
        }

        await using var stream = file.OpenReadStream();
        var result = await importService.ImportAsync(id, stream, cancellationToken);
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
        [FromRoute] BankAccountId id,
        [FromServices] IBankAccountService bankAccountService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankAccountService.DeleteAsync(id, cancellationToken);
        return result.ToNoContent();
    }
}
