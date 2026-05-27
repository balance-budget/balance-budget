using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using FluentValidation;

namespace Balance.Web.Endpoints.JournalEntries;

/// <summary>
/// Full-body PUT shape per ADR 0016. Lines are ordered; each carries an optional
/// <see cref="ReplaceJournalLineRequest.Id"/> (omit to insert a new line, server assigns the id).
/// Body-supplied <see cref="BankTransactionId"/> and per-line
/// <see cref="ReplaceJournalLineRequest.ReconciliationStatus"/> are validated to match current —
/// the PUT does not mutate either.
/// </summary>
internal sealed record ReplaceJournalEntryRequest(
    DateOnly Date,
    string? Description,
    BankTransactionId? BankTransactionId,
    CounterpartyId? CounterpartyId,
    IReadOnlyList<ReplaceJournalLineRequest> Lines
);

internal sealed record ReplaceJournalLineRequest(
    JournalLineId? Id,
    AccountId AccountId,
    long Amount,
    string? Description,
    ReconciliationStatus? ReconciliationStatus
);

internal sealed class ReplaceJournalEntryRequestValidator
    : AbstractValidator<ReplaceJournalEntryRequest>
{
    public ReplaceJournalEntryRequestValidator()
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
