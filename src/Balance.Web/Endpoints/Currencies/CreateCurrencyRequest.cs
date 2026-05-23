using Balance.Data.Entities.Ids;
using Balance.Web.Filters;
using FluentValidation;

namespace Balance.Web.Endpoints.Currencies;

internal sealed record CreateCurrencyRequest(
    CurrencyCode Code,
    string Name,
    int MinorUnitScale,
    string? Symbol
);

internal sealed class CreateCurrencyRequestValidator : AbstractValidator<CreateCurrencyRequest>
{
    public CreateCurrencyRequestValidator()
    {
        RuleFor(x => x.Code.Value).IsCurrencyCode();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.MinorUnitScale).InclusiveBetween(0, 30);
        RuleFor(x => x.Symbol!).MaximumLength(8).When(x => x.Symbol is not null);
    }
}
