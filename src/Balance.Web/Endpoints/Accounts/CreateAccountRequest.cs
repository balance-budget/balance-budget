using System.Text.RegularExpressions;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Web.Filters;
using FluentValidation;

namespace Balance.Web.Endpoints.Accounts;

internal sealed record CreateAccountRequest(
    string Name,
    string Code,
    AccountType AccountType,
    CurrencyCode CurrencyCode,
    bool IsPostable = true,
    bool IsLiquid = true,
    AccountId? ParentAccountId = null,
    string? IconName = null
);

internal sealed partial class CreateAccountRequestValidator
    : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.AccountType).IsInEnum();
        RuleFor(x => x.CurrencyCode.Value).IsCurrencyCode();
        RuleFor(x => x.IconName!)
            .MaximumLength(64)
            .Matches(IconNameRegex())
            .WithMessage("IconName must be a kebab-case icon identifier (e.g. 'piggy-bank').")
            .When(x => !string.IsNullOrWhiteSpace(x.IconName));
    }

    // Shape-only validation, deliberately no allowlist: the SPA's curated icon registry is the
    // single source of truth for which icons exist, and an unknown stored name simply renders the
    // AccountType's default icon. Mirroring the registry here would force a backend change for
    // every icon added client-side, with no user-visible payoff.
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    internal static partial Regex IconNameRegex();
}

internal sealed class UpdateAccountInputValidator : AbstractValidator<UpdateAccountInput>
{
    public UpdateAccountInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.AccountType).IsInEnum();
        RuleFor(x => x.CurrencyCode.Value).IsCurrencyCode();
        RuleFor(x => x.IconName!)
            .MaximumLength(64)
            .Matches(CreateAccountRequestValidator.IconNameRegex())
            .WithMessage("IconName must be a kebab-case icon identifier (e.g. 'piggy-bank').")
            .When(x => !string.IsNullOrWhiteSpace(x.IconName));
    }
}
