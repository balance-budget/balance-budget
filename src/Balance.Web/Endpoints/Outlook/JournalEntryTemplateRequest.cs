using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using FluentValidation;

namespace Balance.Web.Endpoints.Outlook;

internal sealed record CreateJournalEntryTemplateRequest(
    string Name,
    AccountId AccountId,
    AccountId? CounterAccountId,
    CounterpartyId? CounterpartyId,
    Cadence Cadence,
    DateOnly AnchorDate,
    DateOnly? EndDate,
    long ExpectedAmount,
    string? MandateId,
    string? SepaCreditorId
);

internal sealed record UpdateJournalEntryTemplateRequest(
    string Name,
    AccountId AccountId,
    AccountId? CounterAccountId,
    CounterpartyId? CounterpartyId,
    Cadence Cadence,
    DateOnly AnchorDate,
    DateOnly? EndDate,
    long ExpectedAmount
);

internal sealed class CreateJournalEntryTemplateRequestValidator
    : AbstractValidator<CreateJournalEntryTemplateRequest>
{
    public CreateJournalEntryTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AccountId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Cadence).IsInEnum();
        RuleFor(x => x.AnchorDate).NotEqual(default(DateOnly));
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.AnchorDate)
            .When(x => x.EndDate.HasValue)
            .WithMessage("EndDate must be on or after AnchorDate.");
        RuleFor(x => x.ExpectedAmount).NotEqual(0L);
        RuleFor(x => x.MandateId).MaximumLength(64);
        RuleFor(x => x.SepaCreditorId).MaximumLength(64);
    }
}

internal sealed class UpdateJournalEntryTemplateRequestValidator
    : AbstractValidator<UpdateJournalEntryTemplateRequest>
{
    public UpdateJournalEntryTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AccountId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Cadence).IsInEnum();
        RuleFor(x => x.AnchorDate).NotEqual(default(DateOnly));
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.AnchorDate)
            .When(x => x.EndDate.HasValue)
            .WithMessage("EndDate must be on or after AnchorDate.");
        RuleFor(x => x.ExpectedAmount).NotEqual(0L);
    }
}

internal static class JournalEntryTemplateRequestMappers
{
    public static CreateJournalEntryTemplateInput ToInput(
        this CreateJournalEntryTemplateRequest request
    ) =>
        new(
            request.Name,
            request.AccountId,
            request.CounterAccountId,
            request.CounterpartyId,
            request.Cadence,
            request.AnchorDate,
            request.EndDate,
            request.ExpectedAmount,
            request.MandateId,
            request.SepaCreditorId
        );

    public static UpdateJournalEntryTemplateInput ToInput(
        this UpdateJournalEntryTemplateRequest request
    ) =>
        new(
            request.Name,
            request.AccountId,
            request.CounterAccountId,
            request.CounterpartyId,
            request.Cadence,
            request.AnchorDate,
            request.EndDate,
            request.ExpectedAmount
        );
}
