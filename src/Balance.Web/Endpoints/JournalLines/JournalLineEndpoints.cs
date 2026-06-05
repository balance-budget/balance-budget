using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.JournalLines;

internal static class JournalLineEndpoints
{
    public const string PathPrefix = "/journal-lines";

    public static void MapJournalLines(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PathPrefix).WithTags("JournalLines");
        group
            .MapPost("/reassign", ReassignAsync)
            .WithValidation<ReassignJournalLinesRequest>()
            .WithName("ReassignJournalLines");
    }

    private static async Task<
        Results<
            NoContent,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > ReassignAsync(
        [FromBody] ReassignJournalLinesRequest request,
        [FromServices] IJournalEntryService journalEntryService,
        CancellationToken cancellationToken
    )
    {
        var result = await journalEntryService.ReassignLinesAsync(
            request.LineIds,
            request.TargetAccountId,
            cancellationToken
        );
        return result.ToNoContent();
    }
}
