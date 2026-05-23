using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Web.Filters;
using FluentValidation;

namespace Balance.Web.Endpoints.Accounts;

internal sealed record CreateAccountRequest(
    string Name,
    AccountType AccountType,
    CurrencyCode CurrencyCode
);

internal sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AccountType).IsInEnum();
        RuleFor(x => x.CurrencyCode.Value).IsCurrencyCode();
    }
}
