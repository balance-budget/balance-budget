using Balance.Services.Contracts;
using FluentValidation;

namespace Balance.Web.Endpoints.Currencies;

internal sealed class UpdateCurrencyInputValidator : AbstractValidator<UpdateCurrencyInput>
{
    public UpdateCurrencyInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Symbol!).MaximumLength(8).When(x => x.Symbol is not null);
    }
}
