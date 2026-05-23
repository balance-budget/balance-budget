using Balance.Services.Contracts;
using Balance.Web.Filters;
using FluentValidation;

namespace Balance.Web.Endpoints.Accounts;

internal sealed class UpdateAccountInputValidator : AbstractValidator<UpdateAccountInput>
{
    public UpdateAccountInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AccountType).IsInEnum();
        RuleFor(x => x.CurrencyCode.Value).IsCurrencyCode();
    }
}
