using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using FluentValidation;

namespace Balance.Web.Endpoints.JournalEntries;

internal sealed record CreateJournalEntryRequest(
    DateOnly Date,
    string? Description,
    BankTransactionId? BankTransactionId,
    CounterpartyId? CounterpartyId,
    IReadOnlyList<CreateJournalLineRequest> Lines
);

internal sealed record CreateJournalLineRequest(
    AccountId AccountId,
    long Amount,
    string? Description
);

internal sealed class CreateJournalEntryRequestValidator
    : AbstractValidator<CreateJournalEntryRequest>
{
    public CreateJournalEntryRequestValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly));
        RuleFor(x => x.Description!)
            .MaximumLength(512)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 2 });
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.AccountId.Value).NotEqual(Guid.Empty);
                line.RuleFor(l => l.Amount).NotEqual(0L).WithMessage("Amount must be non-zero.");
                line.RuleFor(l => l.Description!)
                    .MaximumLength(512)
                    .When(l => !string.IsNullOrWhiteSpace(l.Description));
            });
    }
}

internal sealed class UpdateJournalEntryInputValidator : AbstractValidator<UpdateJournalEntryInput>
{
    public UpdateJournalEntryInputValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly));
        RuleFor(x => x.Description!)
            .MaximumLength(512)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines.Values)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.Description!)
                    .MaximumLength(512)
                    .When(l => !string.IsNullOrWhiteSpace(l.Description));
            });
    }
}
