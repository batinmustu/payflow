using FluentValidation;

namespace PayFlow.Transaction.Application.Refunds.RequestRefund;

public sealed class RequestRefundCommandValidator : AbstractValidator<RequestRefundCommand>
{
    public RequestRefundCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEqual(Guid.Empty).WithErrorCode("REQUIRED");
        RuleFor(x => x.TransactionId).NotEqual(Guid.Empty).WithErrorCode("REQUIRED");
        RuleFor(x => x.AmountMinor).GreaterThan(0).WithErrorCode("MUST_BE_POSITIVE");
        RuleFor(x => x.RequestedBy)
            .NotEmpty().WithErrorCode("REQUIRED")
            .MaximumLength(128).WithErrorCode("TOO_LONG");
    }
}
