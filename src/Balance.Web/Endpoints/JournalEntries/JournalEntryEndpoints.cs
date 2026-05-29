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
        // PATCH stays mounted as the legacy ADR 0005 surface (kept to avoid breaking existing
        // bookmarks / clients) but is hidden from the OpenAPI document — ADR 0016 makes the
        // PUT below the canonical edit surface, and the SPA no longer calls PATCH.
        group
            .MapPatchSnapshotted<
                JournalEntryId,
                IJournalEntryService,
                UpdateJournalEntryInput,
                JournalEntryDetailOutput
            >(
                "/{id}",
                (svc, id, ct) => svc.GetSnapshotAsync(id, ct),
                (svc, id, input, ct) => svc.UpdateAsync(id, input, ct)
            )
            .ExcludeFromDescription()
            .WithName("UpdateJournalEntry");
        group
            .MapPut("/{id}", ReplaceAsync)
            .WithValidation<ReplaceJournalEntryRequest>()
            .WithName("ReplaceJournalEntry");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteJournalEntry");
    }

    private static async Task<Ok<PagedOutput<JournalEntryOutput>>> ListAsync(
        [AsParameters] ListJournalEntriesRequest request,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        var skip = request.Skip ?? 0;
        var take = request.Take ?? ListJournalEntriesRequest.DefaultPageSize;
        var entries = await journalEntryService.ListAsync(skip, take, request.Q, cancellationToken);
        return TypedResults.Ok(entries);
    }

    private static async Task<
        Results<Ok<JournalEntryDetailOutput>, NotFound<ProblemDetails>, ValidationProblem>
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
            Created<JournalEntryDetailOutput>,
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
                request.CounterpartyId,
                lineInputs
            ),
            cancellationToken
        );

        return result.ToCreatedAt(PathPrefix, v => v.Id.Value);
    }

    private static async Task<
        Results<
            Ok<JournalEntryDetailOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > ReplaceAsync(
        [FromRoute] JournalEntryId id,
        [FromBody] ReplaceJournalEntryRequest request,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<ReplaceJournalLineInput> lineInputs =
        [
            .. request.Lines.Select(l => new ReplaceJournalLineInput(
                l.Id,
                l.AccountId,
                l.Amount,
                l.Description,
                l.ReconciliationStatus
            )),
        ];

        var result = await journalEntryService.ReplaceAsync(
            id,
            new ReplaceJournalEntryInput(
                request.Date,
                request.Description,
                request.CounterpartyId,
                lineInputs
            ),
            cancellationToken
        );

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
        [FromRoute] JournalEntryId id,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        var result = await journalEntryService.DeleteAsync(id, cancellationToken);
        return result.ToNoContent();
    }
}
