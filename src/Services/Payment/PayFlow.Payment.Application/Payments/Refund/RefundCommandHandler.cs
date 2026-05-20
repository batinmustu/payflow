using MediatR;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Domain;
using PayFlow.SharedKernel;

namespace PayFlow.Payment.Application.Payments.Refund;

internal sealed class RefundCommandHandler
    : IRequestHandler<RefundCommand, Result<RefundResponse>>
{
    private readonly IPaymentProviderFactory _factory;

    public RefundCommandHandler(IPaymentProviderFactory factory)
    {
        _factory = factory;
    }

    public async Task<Result<RefundResponse>> Handle(RefundCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var requestResult = RefundRequest.Create(
            command.TenantId,
            command.TransactionId,
            command.ProviderCode,
            command.ProviderReference,
            command.AmountMinor,
            command.Currency,
            command.IdempotencyKey);

        if (requestResult.IsFailure)
        {
            return Result.Failure<RefundResponse>(requestResult.ErrorCode!);
        }

        var providerResult = _factory.Resolve(command.ProviderCode);
        if (providerResult.IsFailure)
        {
            return Result.Failure<RefundResponse>(providerResult.ErrorCode!);
        }

        var result = await providerResult.Value.RefundAsync(requestResult.Value, ct);

        return Result.Success(new RefundResponse(
            ProviderCode: result.ProviderCode,
            Status: result.Status,
            ProviderReference: result.ProviderReference,
            DeclineCode: result.DeclineCode,
            LatencyMilliseconds: result.LatencyMilliseconds));
    }
}
