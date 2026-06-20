using FluentValidation;

namespace Balance.Web.Endpoints.Auth;

// Display preferences are opaque to the backend (ADR-0022): the SPA owns the
// vocabulary. We validate length only so a malformed client can't write
// oversized values, and leave meaning to the frontend.
internal sealed record UpdateUserPreferencesRequest(
    string? Language,
    string? DateFormat,
    string? NumberFormat,
    string? Theme
);

internal sealed class UpdateUserPreferencesRequestValidator
    : AbstractValidator<UpdateUserPreferencesRequest>
{
    public UpdateUserPreferencesRequestValidator()
    {
        RuleFor(x => x.Language).MaximumLength(16);
        RuleFor(x => x.DateFormat).MaximumLength(16);
        RuleFor(x => x.NumberFormat).MaximumLength(16);
        RuleFor(x => x.Theme).MaximumLength(16);
    }
}
