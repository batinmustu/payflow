using FluentValidation;

namespace PayFlow.Identity.Application.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty().WithErrorCode("REQUIRED");
        RuleFor(x => x.Email).NotEmpty().WithErrorCode("REQUIRED");
        // Length rules on the password during login deliberately omitted —
        // we never want to leak the policy through validation noise here.
        RuleFor(x => x.Password).NotEmpty().WithErrorCode("REQUIRED");
    }
}
