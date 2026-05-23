using FluentValidation;

namespace Balance.Web.Filters;

internal static class CurrencyCodeValidationExtensions
{
    // Currency codes are stored in their canonical ISO 4217 form (uppercase ASCII, optional
    // digit suffix). Enforcing the shape at the validator boundary keeps user-supplied codes
    // aligned with the seeded Currencies rows and the FK constraints that reference them.
    public static IRuleBuilderOptions<T, string> IsCurrencyCode<T>(
        this IRuleBuilder<T, string> rule
    ) =>
        rule.NotEmpty()
            .Length(2, 8)
            .Matches("^[A-Z][A-Z0-9]*$")
            .WithMessage("'{PropertyName}' must be 2-8 uppercase ASCII letters or digits.");
}
