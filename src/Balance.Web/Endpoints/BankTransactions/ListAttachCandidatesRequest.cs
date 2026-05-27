using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.BankTransactions;

internal sealed record ListAttachCandidatesRequest([FromQuery] int? DateWindowDays)
{
    public const int DefaultDateWindowDays = 14;
    public const int MaxDateWindowDays = 365;
}

internal sealed class ListAttachCandidatesRequestValidator
    : AbstractValidator<ListAttachCandidatesRequest>
{
    public ListAttachCandidatesRequestValidator()
    {
        RuleFor(x => x.DateWindowDays!.Value)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(ListAttachCandidatesRequest.MaxDateWindowDays)
            .When(x => x.DateWindowDays is not null);
    }
}
