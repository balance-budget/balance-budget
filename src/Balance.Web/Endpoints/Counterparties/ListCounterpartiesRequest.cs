using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Counterparties;

internal sealed record ListCounterpartiesRequest(
    [FromQuery] int? Skip,
    [FromQuery] int? Take,
    [FromQuery] string? Q
)
{
    public const int MaxPageSize = 200;
    public const int MaxSearchLength = 200;
}

internal sealed class ListCounterpartiesRequestValidator
    : AbstractValidator<ListCounterpartiesRequest>
{
    public ListCounterpartiesRequestValidator()
    {
        RuleFor(x => x.Skip!.Value).GreaterThanOrEqualTo(0).When(x => x.Skip is not null);
        RuleFor(x => x.Take!.Value)
            .GreaterThan(0)
            .LessThanOrEqualTo(ListCounterpartiesRequest.MaxPageSize)
            .When(x => x.Take is not null);
        RuleFor(x => x.Q!)
            .MaximumLength(ListCounterpartiesRequest.MaxSearchLength)
            .When(x => x.Q is not null);
    }
}
