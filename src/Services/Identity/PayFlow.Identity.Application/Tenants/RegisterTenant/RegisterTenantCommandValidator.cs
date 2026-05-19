using FluentValidation;

namespace PayFlow.Identity.Application.Tenants.RegisterTenant;

/// <summary>
/// Shape-level validation. The handler enforces business rules (uniqueness,
/// VO format) on top of this. Both layers feed the same response shape:
/// 422 ValidationProblem with errors keyed by field name, each value an
/// array of stable error codes (see docs/api/errors.md).
/// </summary>
public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("REQUIRED")
            .MaximumLength(100).WithErrorCode("TOO_LONG");

        RuleFor(x => x.Slug)
            .NotEmpty().WithErrorCode("REQUIRED")
            .MaximumLength(50).WithErrorCode("TOO_LONG");

        RuleFor(x => x.AdminEmail)
            .NotEmpty().WithErrorCode("REQUIRED")
            .MaximumLength(254).WithErrorCode("TOO_LONG");

        RuleFor(x => x.AdminPassword)
            .NotEmpty().WithErrorCode("REQUIRED")
            .MinimumLength(8).WithErrorCode("TOO_SHORT")
            .MaximumLength(128).WithErrorCode("TOO_LONG");

        RuleFor(x => x.AdminDisplayName)
            .NotEmpty().WithErrorCode("REQUIRED")
            .MaximumLength(100).WithErrorCode("TOO_LONG");
    }
}
