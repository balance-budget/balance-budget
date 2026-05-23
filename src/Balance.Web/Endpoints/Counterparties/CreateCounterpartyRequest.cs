using Balance.Services.Contracts;
using FluentValidation;

namespace Balance.Web.Endpoints.Counterparties;

internal sealed record CreateCounterpartyRequest(string Name);

internal sealed class CreateCounterpartyRequestValidator
    : AbstractValidator<CreateCounterpartyRequest>
{
    public CreateCounterpartyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}

internal sealed class UpdateCounterpartyInputValidator : AbstractValidator<UpdateCounterpartyInput>
{
    public UpdateCounterpartyInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}
