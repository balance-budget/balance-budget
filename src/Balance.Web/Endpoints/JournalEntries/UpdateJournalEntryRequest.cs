using Balance.Data.Entities.Ids;
using FluentValidation;

namespace Balance.Web.Endpoints.JournalEntries;

internal sealed record UpdateJournalEntryRequest(
    DateOnly? Date,
    string? Description,
    BankTransactionId? BankTransactionId,
    CounterpartyId? CounterpartyId,
    IReadOnlyList<CreateJournalLineRequest>? Lines
);

internal sealed class UpdateJournalEntryRequestValidator
    : AbstractValidator<UpdateJournalEntryRequest>
{
    public UpdateJournalEntryRequestValidator()
    {
        RuleFor(x => x.Date!.Value).NotEqual(default(DateOnly)).When(x => x.Date is not null);
        RuleFor(x => x.Description!)
            .MaximumLength(512)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Lines!).Must(l => l.Count >= 2).When(x => x.Lines is not null);
        When(
            x => x.Lines is not null,
            () =>
                RuleForEach(x => x.Lines!)
                    .ChildRules(line =>
                    {
                        line.RuleFor(l => l.AccountId.Value).NotEqual(Guid.Empty);
                        line.RuleFor(l => l.Amount)
                            .NotEqual(0L)
                            .WithMessage("Amount must be non-zero.");
                        line.RuleFor(l => l.Description!)
                            .MaximumLength(512)
                            .When(l => !string.IsNullOrWhiteSpace(l.Description));
                    })
        );
    }
}
