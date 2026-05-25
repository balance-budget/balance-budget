using Balance.Services.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.BankTransactions;

internal sealed record ListBankTransactionsRequest(
    [FromQuery] int? Skip,
    [FromQuery] int? Take,
    [FromQuery] BankTransactionListFilter? Filter
)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
}

internal sealed class ListBankTransactionsRequestValidator
    : AbstractValidator<ListBankTransactionsRequest>
{
    public ListBankTransactionsRequestValidator()
    {
        RuleFor(x => x.Skip!.Value).GreaterThanOrEqualTo(0).When(x => x.Skip is not null);
        RuleFor(x => x.Take!.Value)
            .GreaterThan(0)
            .LessThanOrEqualTo(ListBankTransactionsRequest.MaxPageSize)
            .When(x => x.Take is not null);
        RuleFor(x => x.Filter!.Value).IsInEnum().When(x => x.Filter is not null);
    }
}
