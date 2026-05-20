using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Accounts;

internal sealed record ListAccountRegisterRequest([FromQuery] int? Skip, [FromQuery] int? Take)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
}

internal sealed class ListAccountRegisterRequestValidator
    : AbstractValidator<ListAccountRegisterRequest>
{
    public ListAccountRegisterRequestValidator()
    {
        RuleFor(x => x.Skip!.Value).GreaterThanOrEqualTo(0).When(x => x.Skip is not null);
        RuleFor(x => x.Take!.Value)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(ListAccountRegisterRequest.MaxPageSize)
            .When(x => x.Take is not null);
    }
}
