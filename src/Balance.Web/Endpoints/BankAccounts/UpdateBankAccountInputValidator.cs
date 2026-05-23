using Balance.Services.Contracts;
using Balance.Web.Filters;
using FluentValidation;

namespace Balance.Web.Endpoints.BankAccounts;

internal sealed class UpdateBankAccountInputValidator : AbstractValidator<UpdateBankAccountInput>
{
    public UpdateBankAccountInputValidator()
    {
        RuleFor(x => x.Iban!)
            .Matches(CreateBankAccountRequestValidator.IbanRegex())
            .WithMessage("Iban must be a valid IBAN.")
            .When(x => !string.IsNullOrWhiteSpace(x.Iban));
        RuleFor(x => x.AccountNumber!)
            .MaximumLength(64)
            .When(x => !string.IsNullOrWhiteSpace(x.AccountNumber));
        RuleFor(x => x.Bic!).MaximumLength(11).When(x => !string.IsNullOrWhiteSpace(x.Bic));
        RuleFor(x => x.BankName!)
            .MaximumLength(128)
            .When(x => !string.IsNullOrWhiteSpace(x.BankName));
        RuleFor(x => x.AccountHolderName!)
            .MaximumLength(128)
            .When(x => !string.IsNullOrWhiteSpace(x.AccountHolderName));
        RuleFor(x => x.CurrencyCode!.Value.Value)
            .IsCurrencyCode()
            .When(x => x.CurrencyCode is not null);
    }
}
