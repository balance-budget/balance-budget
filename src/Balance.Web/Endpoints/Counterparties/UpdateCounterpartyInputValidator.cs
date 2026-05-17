using Balance.Services.Contracts;
using FluentValidation;

namespace Balance.Web.Endpoints.Counterparties;

internal sealed class UpdateCounterpartyInputValidator : AbstractValidator<UpdateCounterpartyInput>
{
    public UpdateCounterpartyInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}
