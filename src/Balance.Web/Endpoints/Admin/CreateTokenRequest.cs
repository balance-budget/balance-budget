using FluentValidation;

namespace Balance.Web.Endpoints.Admin;

internal sealed record CreateTokenRequest(string Name, DateTime? ExpiresAt);

internal sealed class CreateTokenRequestValidator : AbstractValidator<CreateTokenRequest>
{
    public CreateTokenRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTime.UtcNow)
            .When(x => x.ExpiresAt.HasValue)
            .WithMessage("ExpiresAt must be in the future.");
    }
}
