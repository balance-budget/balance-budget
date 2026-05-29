using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.BankTransactions;

internal static class BankTransactionEndpoints
{
    public const string PathPrefix = "/bank-transactions";

    public static void MapBankTransactions(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("BankTransactions");
        group
            .MapGet("", ListAsync)
            .WithValidation<ListBankTransactionsRequest>()
            .WithName("ListBankTransactions");
        group.MapGet("/{id}", GetAsync).WithName("GetBankTransaction");
        group
            .MapPost("", CreateAsync)
            .WithValidation<CreateBankTransactionRequest>()
            .WithName("CreateBankTransaction");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteBankTransaction");
        group
            .MapPost("/{id}/dismiss", DismissAsync)
            .WithValidation<DismissBankTransactionRequest>()
            .WithName("DismissBankTransaction");
        group.MapPost("/{id}/undismiss", UndismissAsync).WithName("UndismissBankTransaction");
        group
            .MapPost("/{id}/categorize", CategorizeAsync)
            .WithValidation<CategorizeBankTransactionRequest>()
            .WithName("CategorizeBankTransaction");
        group
            .MapPost("/{id}/attach", AttachAsync)
            .WithValidation<AttachBankTransactionRequest>()
            .WithName("AttachBankTransaction");
        group.MapPost("/{id}/detach", DetachAsync).WithName("DetachBankTransaction");
        group
            .MapGet("/{id}/attach-candidates", ListAttachCandidatesAsync)
            .WithValidation<ListAttachCandidatesRequest>()
            .WithName("ListAttachCandidates");
    }

    private static async Task<Ok<PagedOutput<BankTransactionOutput>>> ListAsync(
        [AsParameters] ListBankTransactionsRequest request,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var skip = request.Skip ?? 0;
        var take = request.Take ?? ListBankTransactionsRequest.DefaultPageSize;
        var filter = request.Filter ?? BankTransactionListFilter.Inbox;
        var bankTransactions = await bankTransactionService.ListAsync(
            skip,
            take,
            filter,
            request.Q,
            cancellationToken
        );
        return TypedResults.Ok(bankTransactions);
    }

    private static async Task<
        Results<Ok<BankTransactionDetailOutput>, NotFound<ProblemDetails>, ValidationProblem>
    > GetAsync(
        [FromRoute] BankTransactionId id,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankTransactionService.GetAsync(id, cancellationToken);
        return result.ToOkReadOnly();
    }

    private static async Task<
        Results<
            Created<BankTransactionOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > CreateAsync(
        [FromBody] CreateBankTransactionRequest request,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankTransactionService.CreateAsync(
            new CreateBankTransactionInput(
                request.BankAccountId,
                request.BookingDate,
                request.Amount,
                request.CurrencyCode,
                request.Description,
                request.CounterpartyName,
                request.CounterpartyAccountNumber
            ),
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
        [FromRoute] BankTransactionId id,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankTransactionService.DeleteAsync(id, cancellationToken);
        return result.ToNoContent();
    }

    private static async Task<
        Results<
            Ok<BankTransactionOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > DismissAsync(
        [FromRoute] BankTransactionId id,
        [FromBody] DismissBankTransactionRequest request,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankTransactionService.DismissAsync(
            id,
            request.Reason,
            cancellationToken
        );
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<BankTransactionOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > UndismissAsync(
        [FromRoute] BankTransactionId id,
        [FromServices] IBankTransactionService bankTransactionService,
        CancellationToken cancellationToken
    )
    {
        var result = await bankTransactionService.UndismissAsync(id, cancellationToken);
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<JournalEntryDetailOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > AttachAsync(
        [FromRoute] BankTransactionId id,
        [FromBody] AttachBankTransactionRequest request,
        [FromServices] IBankTransactionAttachService attachService,
        CancellationToken cancellationToken
    )
    {
        var result = await attachService.AttachAsync(id, request.JournalEntryId, cancellationToken);
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<JournalEntryDetailOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > DetachAsync(
        [FromRoute] BankTransactionId id,
        [FromServices] IBankTransactionAttachService attachService,
        CancellationToken cancellationToken
    )
    {
        var result = await attachService.DetachAsync(id, cancellationToken);
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<IReadOnlyList<AttachCandidateOutput>>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > ListAttachCandidatesAsync(
        [FromRoute] BankTransactionId id,
        [AsParameters] ListAttachCandidatesRequest request,
        [FromServices] IBankTransactionAttachService attachService,
        CancellationToken cancellationToken
    )
    {
        var days = request.DateWindowDays ?? ListAttachCandidatesRequest.DefaultDateWindowDays;
        var result = await attachService.ListCandidatesAsync(id, days, cancellationToken);
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<JournalEntryDetailOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > CategorizeAsync(
        [FromRoute] BankTransactionId id,
        [FromBody] CategorizeBankTransactionRequest request,
        [FromServices] IBankTransactionCategorisationService categorisationService,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<CategorizeBankTransactionLineInput> lines =
        [
            .. request.Lines.Select(l => new CategorizeBankTransactionLineInput(
                l.AccountId,
                l.Amount,
                l.Description
            )),
        ];

        var result = await categorisationService.CategorizeAsync(
            id,
            new CategorizeBankTransactionInput(
                CounterpartyId: request.CounterpartyId,
                NewCounterparty: request.NewCounterparty is null
                    ? null
                    : new NewCounterpartyInput(request.NewCounterparty.Name),
                Date: request.Date,
                Description: request.Description,
                Lines: lines
            ),
            cancellationToken
        );
        return result.ToOk();
    }
}
