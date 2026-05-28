using FluentValidation;

namespace Balance.Web.Endpoints.Auth;

internal sealed record SetupRequest(
    string Email,
    string Password,
    string DisplayName,
    string? SetupToken
);

internal sealed class SetupRequestValidator : AbstractValidator<SetupRequest>
{
    public SetupRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(128);
    }
}
