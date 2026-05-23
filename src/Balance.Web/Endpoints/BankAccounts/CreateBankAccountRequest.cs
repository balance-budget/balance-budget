using System.Text.RegularExpressions;
using Balance.Data.Entities.Ids;
using Balance.Web.Filters;
using FluentValidation;

namespace Balance.Web.Endpoints.BankAccounts;

internal sealed record CreateBankAccountRequest(
    string? Iban,
    string? AccountNumber,
    string? Bic,
    string? BankName,
    string? AccountHolderName,
    CurrencyCode? CurrencyCode,
    AccountId? AccountId,
    CounterpartyId? CounterpartyId
);

internal sealed partial class CreateBankAccountRequestValidator
    : AbstractValidator<CreateBankAccountRequest>
{
    public CreateBankAccountRequestValidator()
    {
        RuleFor(x => x.Iban!)
            .Matches(IbanRegex())
            .WithMessage("Iban must be a valid IBAN.")
            .When(x => !string.IsNullOrWhiteSpace(x.Iban));
        RuleFor(x => x.AccountNumber!)
            .MaximumLength(64)
            .When(x => !string.IsNullOrWhiteSpace(x.AccountNumber));
        RuleFor(x => x.Bic!).MaximumLength(11).When(x => !string.IsNullOrWhiteSpace(x.Bic));
        RuleFor(x => x.BankName!)
            .MaximumLength(128)
            .When(x => !string.IsNullOrWhiteSpace(x.BankName));
        RuleFor(x => x.AccountHolderName!)
            .MaximumLength(128)
            .When(x => !string.IsNullOrWhiteSpace(x.AccountHolderName));
        RuleFor(x => x.CurrencyCode!.Value.Value)
            .IsCurrencyCode()
            .When(x => x.CurrencyCode is not null);
    }

    [GeneratedRegex(@"^[A-Z]{2}[0-9]{2}[A-Z0-9]{1,30}$", RegexOptions.CultureInvariant)]
    internal static partial Regex IbanRegex();
}
