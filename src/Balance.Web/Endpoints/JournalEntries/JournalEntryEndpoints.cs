using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.JournalEntries;

internal static class JournalEntryEndpoints
{
    public const string PathPrefix = "/journal-entries";

    public static void MapJournalEntries(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("JournalEntries");
        group
            .MapGet("", ListAsync)
            .WithValidation<ListJournalEntriesRequest>()
            .WithName("ListJournalEntries");
        group.MapGet("/{id}", GetAsync).WithName("GetJournalEntry");
        group
            .MapPost("", CreateAsync)
            .WithValidation<CreateJournalEntryRequest>()
            .WithName("CreateJournalEntry");
        group
            .MapPatchSnapshotted<
                JournalEntryId,
                IJournalEntryService,
                UpdateJournalEntryInput,
                JournalEntryOutput
            >(
                "/{id}",
                (svc, id, ct) => svc.GetSnapshotAsync(id, ct),
                (svc, id, input, ct) => svc.UpdateAsync(id, input, ct)
            )
            .WithName("UpdateJournalEntry");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteJournalEntry");
    }

    private static async Task<Ok<IReadOnlyList<JournalEntryRowOutput>>> ListAsync(
        [AsParameters] ListJournalEntriesRequest request,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        var skip = request.Skip ?? 0;
        var take = request.Take ?? ListJournalEntriesRequest.DefaultPageSize;
        var entries = await journalEntryService.ListAsync(skip, take, cancellationToken);
        return TypedResults.Ok(entries);
    }

    private static async Task<
        Results<Ok<JournalEntryOutput>, NotFound<ProblemDetails>, ValidationProblem>
    > GetAsync(
        [FromRoute] JournalEntryId id,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        var result = await journalEntryService.GetAsync(id, cancellationToken);
        return result.ToOkReadOnly();
    }

    private static async Task<
        Results<
            Created<JournalEntryOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > CreateAsync(
        [FromBody] CreateJournalEntryRequest request,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<CreateJournalLineInput> lineInputs =
        [
            .. request.Lines.Select(l => new CreateJournalLineInput(
                l.AccountId,
                l.Amount,
                l.Description
            )),
        ];

        var result = await journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                request.Date,
                request.Description,
                request.BankTransactionId,
                request.CounterpartyId,
                lineInputs
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
        [FromRoute] JournalEntryId id,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        var result = await journalEntryService.DeleteAsync(id, cancellationToken);
        return result.ToNoContent();
    }
}
