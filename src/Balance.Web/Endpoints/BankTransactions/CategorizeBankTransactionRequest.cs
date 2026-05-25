using Balance.Data.Entities.Ids;
using FluentValidation;

namespace Balance.Web.Endpoints.BankTransactions;

internal sealed record CategorizeBankTransactionRequest(
    CounterpartyId? CounterpartyId,
    NewCounterpartyRequest? NewCounterparty,
    DateOnly Date,
    string? Description,
    IReadOnlyList<CategorizeBankTransactionLineRequest> Lines
);

internal sealed record NewCounterpartyRequest(string Name);

internal sealed record CategorizeBankTransactionLineRequest(
    AccountId AccountId,
    long Amount,
    string? Description
);

internal sealed class CategorizeBankTransactionRequestValidator
    : AbstractValidator<CategorizeBankTransactionRequest>
{
    public CategorizeBankTransactionRequestValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly));
        RuleFor(x => x.Description!)
            .MaximumLength(512)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 1 });
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.AccountId.Value).NotEqual(Guid.Empty);
                line.RuleFor(l => l.Amount).NotEqual(0L).WithMessage("Amount must be non-zero.");
                line.RuleFor(l => l.Description!)
                    .MaximumLength(512)
                    .When(l => !string.IsNullOrWhiteSpace(l.Description));
            });
        When(
            x => x.NewCounterparty is not null,
            () =>
            {
                RuleFor(x => x.NewCounterparty!.Name)
                    .NotEmpty()
                    .Must(n => !string.IsNullOrWhiteSpace(n))
                    .MaximumLength(200);
            }
        );
    }
}
