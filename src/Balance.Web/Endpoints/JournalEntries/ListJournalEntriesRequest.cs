using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.JournalEntries;

internal sealed record ListJournalEntriesRequest(
    [FromQuery] int? Skip,
    [FromQuery] int? Take,
    [FromQuery] string? Q
)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
    public const int MaxSearchLength = 200;
}

internal sealed class ListJournalEntriesRequestValidator
    : AbstractValidator<ListJournalEntriesRequest>
{
    public ListJournalEntriesRequestValidator()
    {
        RuleFor(x => x.Skip!.Value).GreaterThanOrEqualTo(0).When(x => x.Skip is not null);
        RuleFor(x => x.Take!.Value)
            .GreaterThan(0)
            .LessThanOrEqualTo(ListJournalEntriesRequest.MaxPageSize)
            .When(x => x.Take is not null);
        RuleFor(x => x.Q!)
            .MaximumLength(ListJournalEntriesRequest.MaxSearchLength)
            .When(x => x.Q is not null);
    }
}
