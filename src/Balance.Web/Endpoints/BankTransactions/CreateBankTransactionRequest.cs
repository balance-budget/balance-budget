using Balance.Data.Entities.Ids;
using Balance.Web.Filters;
using FluentValidation;

namespace Balance.Web.Endpoints.BankTransactions;

internal sealed record CreateBankTransactionRequest(
    BankAccountId BankAccountId,
    DateOnly BookingDate,
    long Amount,
    CurrencyCode CurrencyCode
);

internal sealed class CreateBankTransactionRequestValidator
    : AbstractValidator<CreateBankTransactionRequest>
{
    public CreateBankTransactionRequestValidator()
    {
        RuleFor(x => x.BankAccountId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.BookingDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Amount).NotEqual(0L).WithMessage("Amount must be non-zero.");
        RuleFor(x => x.CurrencyCode.Value).IsCurrencyCode();
    }
}
