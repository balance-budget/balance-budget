using FluentValidation;

namespace Balance.Web.Endpoints.Admin;

internal sealed record CreateUserRequest(string Email, string Password, string DisplayName);

internal sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(128);
    }
}
