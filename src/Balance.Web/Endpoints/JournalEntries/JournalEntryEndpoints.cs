using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
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
            .MapPatch("/{id}", UpdateAsync)
            .WithValidation<UpdateJournalEntryRequest>()
            .WithName("UpdateJournalEntry");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteJournalEntry");
    }

    private static async Task<Ok<IReadOnlyList<JournalEntryOutput>>> ListAsync(
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

    private static async Task<Results<Ok<JournalEntryOutput>, NotFound>> GetAsync(
        [FromRoute] JournalEntryId id,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        var entry = await journalEntryService.GetAsync(id, cancellationToken);
        return entry is null ? TypedResults.NotFound() : TypedResults.Ok(entry);
    }

    private static async Task<Created<JournalEntryOutput>> CreateAsync(
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

        var entry = await journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                request.Date,
                request.Description,
                request.BankTransactionId,
                request.CounterpartyId,
                lineInputs
            ),
            cancellationToken
        );

        return TypedResults.Created($"{PathPrefix}/{entry.Id.Value}", entry);
    }

    private static async Task<Ok<JournalEntryOutput>> UpdateAsync(
        [FromRoute] JournalEntryId id,
        [FromBody] UpdateJournalEntryRequest request,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<CreateJournalLineInput>? lineInputs = request.Lines is null
            ? null
            :
            [
                .. request.Lines.Select(l => new CreateJournalLineInput(
                    l.AccountId,
                    l.Amount,
                    l.Description
                )),
            ];

        var entry = await journalEntryService.UpdateAsync(
            id,
            new UpdateJournalEntryInput(
                request.Date,
                request.Description,
                request.BankTransactionId,
                request.CounterpartyId,
                lineInputs
            ),
            cancellationToken
        );

        return TypedResults.Ok(entry);
    }

    private static async Task<NoContent> DeleteAsync(
        [FromRoute] JournalEntryId id,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        await journalEntryService.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
