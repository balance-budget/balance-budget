using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using FluentValidation;

namespace Balance.Web.Endpoints.Outlook;

/// <summary>
/// POST body for the Outlook projection: an optional ephemeral what-if <c>Scenario</c> (ADR-0027),
/// never persisted. POST rather than GET because the scenario is a structured payload.
/// </summary>
internal sealed record OutlookProjectionRequest(OutlookScenarioRequest? Scenario);

internal sealed record OutlookScenarioRequest(
    IReadOnlyList<JournalEntryTemplateId> DisabledTemplateIds,
    IReadOnlyList<OutlookScenarioTemplateRequest> AddedTemplates,
    IReadOnlyList<OutlookScenarioAmountOverrideRequest> AmountOverrides
);

internal sealed record OutlookScenarioTemplateRequest(
    AccountId AccountId,
    Cadence Cadence,
    DateOnly AnchorDate,
    DateOnly? EndDate,
    long ExpectedAmount
);

internal sealed record OutlookScenarioAmountOverrideRequest(
    JournalEntryTemplateId TemplateId,
    long ExpectedAmount
);

internal sealed class OutlookProjectionRequestValidator
    : AbstractValidator<OutlookProjectionRequest>
{
    public OutlookProjectionRequestValidator()
    {
        When(
            x => x.Scenario is not null,
            () =>
            {
                RuleFor(x => x.Scenario!.DisabledTemplateIds).NotNull();
                RuleFor(x => x.Scenario!.AddedTemplates).NotNull();
                RuleFor(x => x.Scenario!.AmountOverrides).NotNull();
                RuleForEach(x => x.Scenario!.AddedTemplates)
                    .ChildRules(added =>
                    {
                        added.RuleFor(a => a.AccountId.Value).NotEqual(Guid.Empty);
                        added.RuleFor(a => a.Cadence).IsInEnum();
                        added.RuleFor(a => a.AnchorDate).NotEqual(default(DateOnly));
                        added.RuleFor(a => a.ExpectedAmount).NotEqual(0L);
                    });
                RuleForEach(x => x.Scenario!.AmountOverrides)
                    .ChildRules(o =>
                    {
                        o.RuleFor(a => a.TemplateId.Value).NotEqual(Guid.Empty);
                        o.RuleFor(a => a.ExpectedAmount).NotEqual(0L);
                    });
            }
        );
    }
}

internal static class OutlookProjectionRequestMappers
{
    public static OutlookScenarioInput? ToInput(this OutlookProjectionRequest request) =>
        request.Scenario is null
            ? null
            : new OutlookScenarioInput(
                [.. request.Scenario.DisabledTemplateIds],
                [
                    .. request.Scenario.AddedTemplates.Select(a => new OutlookScenarioTemplateInput(
                        a.AccountId,
                        a.Cadence,
                        a.AnchorDate,
                        a.EndDate,
                        a.ExpectedAmount
                    )),
                ],
                [
                    .. request.Scenario.AmountOverrides.Select(
                        o => new OutlookScenarioAmountOverrideInput(o.TemplateId, o.ExpectedAmount)
                    ),
                ]
            );
}
