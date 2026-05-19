using MediatR;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Domain;
using PayFlow.SharedKernel;

namespace PayFlow.Payment.Application.Payments.Charge;

internal sealed class ChargeCommandHandler
    : IRequestHandler<ChargeCommand, Result<ChargeResponse>>
{
    private readonly IPaymentProviderFactory _factory;

    public ChargeCommandHandler(IPaymentProviderFactory factory)
    {
        _factory = factory;
    }

    public async Task<Result<ChargeResponse>> Handle(ChargeCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var requestResult = PaymentRequest.Create(
            command.TenantId,
            command.TransactionId,
            command.ProviderCode,
            command.AmountMinor,
            command.Currency,
            command.CardToken);

        if (requestResult.IsFailure)
        {
            return Result.Failure<ChargeResponse>(requestResult.ErrorCode!);
        }

        var providerResult = _factory.Resolve(command.ProviderCode);
        if (providerResult.IsFailure)
        {
            return Result.Failure<ChargeResponse>(providerResult.ErrorCode!);
        }

        var result = await providerResult.Value.ChargeAsync(requestResult.Value, ct);

        return Result.Success(new ChargeResponse(
            PaymentId: result.PaymentId,
            ProviderCode: result.ProviderCode,
            Status: result.Status,
            ProviderReference: result.ProviderReference,
            DeclineCode: result.DeclineCode,
            LatencyMilliseconds: result.LatencyMilliseconds));
    }
}
