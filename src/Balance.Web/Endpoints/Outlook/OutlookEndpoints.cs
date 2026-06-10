using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using Balance.Web.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Outlook;

internal static class OutlookEndpoints
{
    public const string TemplatesPathPrefix = "/outlook/templates";

    // Hardcoded fallback until per-user currency preferences land (matches the Dashboard).
    private static readonly CurrencyCode DefaultCurrency = new("EUR");

    private const int DefaultHorizonMonths = 12;

    public static void MapOutlook(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/outlook").WithTags("Outlook");

        group.MapGet("/templates", ListTemplatesAsync).WithName("ListJournalEntryTemplates");
        group.MapGet("/templates/{id}", GetTemplateAsync).WithName("GetJournalEntryTemplate");
        group
            .MapPost("/templates", CreateTemplateAsync)
            .WithValidation<CreateJournalEntryTemplateRequest>()
            .WithName("CreateJournalEntryTemplate");
        group
            .MapPut("/templates/{id}", UpdateTemplateAsync)
            .WithValidation<UpdateJournalEntryTemplateRequest>()
            .WithName("UpdateJournalEntryTemplate");
        group
            .MapDelete("/templates/{id}", DeleteTemplateAsync)
            .WithName("DeleteJournalEntryTemplate");
        group.MapGet("/candidates", DetectCandidatesAsync).WithName("DetectTemplateCandidates");

        group.MapGet("/projection", GetProjectionAsync).WithName("GetOutlookProjection");
        group
            .MapPost("/projection", PostProjectionAsync)
            .WithValidation<OutlookProjectionRequest>()
            .WithName("GetOutlookProjectionWithScenario");
    }

    private static async Task<Ok<IReadOnlyList<JournalEntryTemplateOutput>>> ListTemplatesAsync(
        [FromServices] IJournalEntryTemplateService service,
        CancellationToken cancellationToken
    )
    {
        var templates = await service.ListAsync(cancellationToken);
        return TypedResults.Ok(templates);
    }

    private static async Task<
        Results<Ok<JournalEntryTemplateOutput>, NotFound<ProblemDetails>, ValidationProblem>
    > GetTemplateAsync(
        [FromRoute] JournalEntryTemplateId id,
        [FromServices] IJournalEntryTemplateService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result.ToOkReadOnly();
    }

    private static async Task<
        Results<
            Created<JournalEntryTemplateOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > CreateTemplateAsync(
        [FromBody] CreateJournalEntryTemplateRequest request,
        [FromServices] IJournalEntryTemplateService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.CreateAsync(request.ToInput(), cancellationToken);
        return result.ToCreatedAt(TemplatesPathPrefix, v => v.Id.Value);
    }

    private static async Task<
        Results<
            Ok<JournalEntryTemplateOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > UpdateTemplateAsync(
        [FromRoute] JournalEntryTemplateId id,
        [FromBody] UpdateJournalEntryTemplateRequest request,
        [FromServices] IJournalEntryTemplateService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.UpdateAsync(id, request.ToInput(), cancellationToken);
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
    > DeleteTemplateAsync(
        [FromRoute] JournalEntryTemplateId id,
        [FromServices] IJournalEntryTemplateService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.ToNoContent();
    }

    private static async Task<Ok<IReadOnlyList<TemplateCandidateOutput>>> DetectCandidatesAsync(
        [FromServices] IJournalEntryTemplateService service,
        CancellationToken cancellationToken
    )
    {
        var candidates = await service.DetectCandidatesAsync(cancellationToken);
        return TypedResults.Ok(candidates);
    }

    private static async Task<
        Results<
            Ok<OutlookProjectionOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > GetProjectionAsync(
        [FromQuery] CurrencyCode? currency,
        [FromQuery] int? horizon,
        [FromServices] IOutlookService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.GetProjectionAsync(
            currency ?? DefaultCurrency,
            horizon ?? DefaultHorizonMonths,
            scenario: null,
            cancellationToken
        );
        return result.ToOk();
    }

    private static async Task<
        Results<
            Ok<OutlookProjectionOutput>,
            NotFound<ProblemDetails>,
            Conflict<ProblemDetails>,
            UnprocessableEntity<ProblemDetails>,
            ValidationProblem
        >
    > PostProjectionAsync(
        [FromQuery] CurrencyCode? currency,
        [FromQuery] int? horizon,
        [FromBody] OutlookProjectionRequest request,
        [FromServices] IOutlookService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.GetProjectionAsync(
            currency ?? DefaultCurrency,
            horizon ?? DefaultHorizonMonths,
            request.ToInput(),
            cancellationToken
        );
        return result.ToOk();
    }
}
