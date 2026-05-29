using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Search;

internal sealed record SearchRequest([FromQuery] string Q)
{
    public const int MinLength = 2;
    public const int MaxLength = 200;
}

internal sealed class SearchRequestValidator : AbstractValidator<SearchRequest>
{
    public SearchRequestValidator()
    {
        RuleFor(x => x.Q)
            .NotEmpty()
            .MinimumLength(SearchRequest.MinLength)
            .MaximumLength(SearchRequest.MaxLength);
    }
}
