using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using FluentValidation;

namespace Balance.Web.Endpoints.Accounts;

internal sealed record UpdateAccountRequest(
    string? Name,
    AccountType? AccountType,
    CurrencyCode? CurrencyCode
);

internal sealed class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        RuleFor(x => x.Name!).NotEmpty().MaximumLength(128).When(x => x.Name is not null);
        RuleFor(x => x.AccountType!.Value).IsInEnum().When(x => x.AccountType is not null);
        RuleFor(x => x.CurrencyCode!.Value.Value)
            .NotEmpty()
            .Length(2, 8)
            .When(x => x.CurrencyCode is not null);
    }
}
