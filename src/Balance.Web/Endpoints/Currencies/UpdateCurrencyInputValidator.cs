using Balance.Services.Contracts;
using Balance.Web.Filters;
using FluentValidation;

namespace Balance.Web.Endpoints.Currencies;

internal sealed class UpdateCurrencyInputValidator : AbstractValidator<UpdateCurrencyInput>
{
    public UpdateCurrencyInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Symbol!).IsCurrencyCode().When(x => x.Symbol is not null);
    }
}
