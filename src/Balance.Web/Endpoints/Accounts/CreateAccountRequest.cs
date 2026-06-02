using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using FluentValidation;

namespace Balance.Web.Endpoints.Accounts;

internal sealed record CreateAccountRequest(
    string Name,
    string Code,
    AccountType AccountType,
    CurrencyCode CurrencyCode,
    bool IsPostable = true,
    AccountId? ParentAccountId = null
);

internal sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.AccountType).IsInEnum();
        RuleFor(x => x.CurrencyCode.Value).IsCurrencyCode();
    }
}

internal sealed class UpdateAccountInputValidator : AbstractValidator<UpdateAccountInput>
{
    public UpdateAccountInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.AccountType).IsInEnum();
        RuleFor(x => x.CurrencyCode.Value).IsCurrencyCode();
    }
}
