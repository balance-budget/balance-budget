using FluentValidation;

namespace Balance.Web.Endpoints.Counterparties;

internal sealed record UpdateCounterpartyRequest(string? Name);

internal sealed class UpdateCounterpartyRequestValidator
    : AbstractValidator<UpdateCounterpartyRequest>
{
    public UpdateCounterpartyRequestValidator()
    {
        RuleFor(x => x.Name!).NotEmpty().MaximumLength(128).When(x => x.Name is not null);
    }
}
