using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;
using Balance.Services.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.JournalEntries;

internal static class JournalEntryEndpoints
{
    public const string PathPrefix = "/journal-entries";

    public static void MapJournalEntries(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("JournalEntries");
        group.MapGet("", ListAsync).WithName("ListJournalEntries");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetJournalEntry");
        group.MapPost("", CreateAsync).WithName("CreateJournalEntry");
        group.MapPatch("/{id:guid}", UpdateAsync).WithName("UpdateJournalEntry");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteJournalEntry");
    }

    private static async Task<Ok<IReadOnlyList<JournalEntryResponse>>> ListAsync(
        [FromServices] IJournalEntryService journalEntryService,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        CancellationToken cancellationToken
    )
    {
        var entries = await journalEntryService.ListAsync(
            skip ?? 0,
            take ?? 0,
            cancellationToken
        );
        IReadOnlyList<JournalEntryResponse> responses =
        [
            .. entries.Select(JournalEntryResponse.From),
        ];
        return TypedResults.Ok(responses);
    }

    private static async Task<Results<Ok<JournalEntryResponse>, NotFound>> GetAsync(
        [FromRoute] Guid id,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        var entry = await journalEntryService.GetAsync(
            new JournalEntryId(id),
            cancellationToken
        );
        return entry is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(JournalEntryResponse.From(entry));
    }

    private static async Task<Created<JournalEntryResponse>> CreateAsync(
        [FromBody] CreateJournalEntryRequest request,
        [FromServices] IJournalEntryService journalEntryService,
        [FromServices] IValidator<CreateJournalEntryRequest> validator,
        CancellationToken cancellationToken
    )
    {
        await ValidateAsync(validator, request, cancellationToken);

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

        var response = JournalEntryResponse.From(entry);
        return TypedResults.Created($"{PathPrefix}/{entry.Id.Value}", response);
    }

    private static async Task<Ok<JournalEntryResponse>> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateJournalEntryRequest request,
        [FromServices] IJournalEntryService journalEntryService,
        [FromServices] IValidator<UpdateJournalEntryRequest> validator,
        CancellationToken cancellationToken
    )
    {
        await ValidateAsync(validator, request, cancellationToken);

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
            new JournalEntryId(id),
            new UpdateJournalEntryInput(
                request.Date,
                request.Description,
                request.BankTransactionId,
                request.CounterpartyId,
                lineInputs
            ),
            cancellationToken
        );

        return TypedResults.Ok(JournalEntryResponse.From(entry));
    }

    private static async Task<NoContent> DeleteAsync(
        [FromRoute] Guid id,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        await journalEntryService.DeleteAsync(new JournalEntryId(id), cancellationToken);
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
