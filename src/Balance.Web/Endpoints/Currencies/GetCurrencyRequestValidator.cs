using Balance.Data.Entities.Ids;
using FluentValidation;

namespace Balance.Web.Endpoints.Currencies;

internal sealed record GetCurrencyRequest(CurrencyCode Code);

internal sealed class GetCurrencyRequestValidator : AbstractValidator<GetCurrencyRequest>
{
    public GetCurrencyRequestValidator()
    {
        RuleFor(x => x.Code.Value).NotEmpty().Length(2, 8);
    }
}
