using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.Loans;
using FluentValidation;

namespace Balance.Web.Endpoints.Loans;

/// <summary>
/// POST body for the projection endpoint: an optional ephemeral what-if scenario (ADR-0025).
/// POST rather than GET because the scenario is a structured payload; nothing is persisted.
/// </summary>
internal sealed record LoanProjectionRequest(LoanScenarioRequest? Scenario);

internal sealed record LoanScenarioRequest(
    IReadOnlyList<LoanScenarioExtraRepaymentRequest> ExtraRepayments,
    ExtraRepaymentPolicy Policy,
    decimal? AssumedAnnualRatePercent
);

internal sealed record LoanScenarioExtraRepaymentRequest(
    LoanPartId LoanPartId,
    DateOnly Date,
    long Amount
);

internal sealed class LoanProjectionRequestValidator : AbstractValidator<LoanProjectionRequest>
{
    public LoanProjectionRequestValidator()
    {
        When(
            x => x.Scenario is not null,
            () =>
            {
                RuleFor(x => x.Scenario!.Policy).IsInEnum();
                RuleFor(x => x.Scenario!.ExtraRepayments).NotNull();
                RuleFor(x => x.Scenario!.AssumedAnnualRatePercent)
                    .InclusiveBetween(0m, 100m)
                    .When(x => x.Scenario!.AssumedAnnualRatePercent.HasValue);
                RuleForEach(x => x.Scenario!.ExtraRepayments)
                    .ChildRules(extra =>
                    {
                        extra.RuleFor(e => e.LoanPartId.Value).NotEqual(Guid.Empty);
                        extra.RuleFor(e => e.Date).NotEqual(default(DateOnly));
                        extra.RuleFor(e => e.Amount).GreaterThan(0L);
                    });
            }
        );
    }
}

internal static class LoanProjectionRequestMappers
{
    public static LoanScenarioInput? ToInput(this LoanProjectionRequest request) =>
        request.Scenario is null
            ? null
            : new LoanScenarioInput(
                [
                    .. request.Scenario.ExtraRepayments.Select(
                        e => new LoanScenarioExtraRepaymentInput(e.LoanPartId, e.Date, e.Amount)
                    ),
                ],
                request.Scenario.Policy,
                request.Scenario.AssumedAnnualRatePercent
            );
}
