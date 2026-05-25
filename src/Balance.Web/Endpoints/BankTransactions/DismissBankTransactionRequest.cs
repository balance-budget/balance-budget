using FluentValidation;

namespace Balance.Web.Endpoints.BankTransactions;

internal sealed record DismissBankTransactionRequest(string Reason);

internal sealed class DismissBankTransactionRequestValidator
    : AbstractValidator<DismissBankTransactionRequest>
{
    public DismissBankTransactionRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Dismissal reason must not be empty.")
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithMessage("Dismissal reason must not be whitespace.")
            .MaximumLength(500);
    }
}
