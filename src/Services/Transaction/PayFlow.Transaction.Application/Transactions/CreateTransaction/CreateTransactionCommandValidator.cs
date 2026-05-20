using FluentValidation;

namespace PayFlow.Transaction.Application.Transactions.CreateTransaction;

public sealed class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEqual(Guid.Empty).WithErrorCode("REQUIRED");
        RuleFor(x => x.OrderReference)
            .NotEmpty().WithErrorCode("REQUIRED")
            .MaximumLength(64).WithErrorCode("TOO_LONG");
        RuleFor(x => x.AmountMinor).GreaterThan(0).WithErrorCode("MUST_BE_POSITIVE");
        RuleFor(x => x.Currency)
            .NotEmpty().WithErrorCode("REQUIRED")
            .Length(3).WithErrorCode("LENGTH_3");
        RuleFor(x => x.CardToken)
            .NotEmpty().WithErrorCode("REQUIRED")
            .MaximumLength(128).WithErrorCode("TOO_LONG");
    }
}
