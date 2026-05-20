using FluentValidation;

namespace PayFlow.Payment.Application.Payments.Refund;

public sealed class RefundCommandValidator : AbstractValidator<RefundCommand>
{
    public RefundCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEqual(Guid.Empty).WithErrorCode("REQUIRED");
        RuleFor(x => x.TransactionId).NotEqual(Guid.Empty).WithErrorCode("REQUIRED");
        RuleFor(x => x.ProviderCode).NotEmpty().WithErrorCode("REQUIRED");
        RuleFor(x => x.ProviderReference).NotEmpty().WithErrorCode("REQUIRED");
        RuleFor(x => x.AmountMinor).GreaterThan(0).WithErrorCode("MUST_BE_POSITIVE");
        RuleFor(x => x.Currency).NotEmpty().WithErrorCode("REQUIRED")
            .Length(3).WithErrorCode("LENGTH_3");
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithErrorCode("REQUIRED");
    }
}
