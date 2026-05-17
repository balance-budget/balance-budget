using FluentValidation;

namespace Balance.Web.Endpoints.Currencies;

internal sealed record UpdateCurrencyRequest(string? Name, string? Symbol);

internal sealed class UpdateCurrencyRequestValidator : AbstractValidator<UpdateCurrencyRequest>
{
    public UpdateCurrencyRequestValidator()
    {
        RuleFor(x => x.Name!).NotEmpty().MaximumLength(64).When(x => x.Name is not null);
        RuleFor(x => x.Symbol!).MaximumLength(8).When(x => x.Symbol is not null);
    }
}
