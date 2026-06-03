using Balance.Services.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.BankAccounts;

internal sealed record ListBankAccountsRequest(
    [FromQuery] int? Skip,
    [FromQuery] int? Take,
    [FromQuery] string? Q,
    [FromQuery] BankAccountOwnerFilter? Owner
)
{
    public const int MaxPageSize = 200;
    public const int MaxSearchLength = 200;
}

internal sealed class ListBankAccountsRequestValidator : AbstractValidator<ListBankAccountsRequest>
{
    public ListBankAccountsRequestValidator()
    {
        RuleFor(x => x.Skip!.Value).GreaterThanOrEqualTo(0).When(x => x.Skip is not null);
        RuleFor(x => x.Take!.Value)
            .GreaterThan(0)
            .LessThanOrEqualTo(ListBankAccountsRequest.MaxPageSize)
            .When(x => x.Take is not null);
        RuleFor(x => x.Owner!.Value).IsInEnum().When(x => x.Owner is not null);
        RuleFor(x => x.Q!)
            .MaximumLength(ListBankAccountsRequest.MaxSearchLength)
            .When(x => x.Q is not null);
    }
}
