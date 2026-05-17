using Balance.Services.Contracts;
using FluentValidation;

namespace Balance.Web.Endpoints.JournalEntries;

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
