using PayFlow.Payment.Application.Abstractions;
using PayFlow.SharedKernel;

namespace PayFlow.Payment.Infrastructure.Providers;

internal sealed class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly Dictionary<string, IPaymentProvider> _byCode;

    public PaymentProviderFactory(IEnumerable<IPaymentProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _byCode = providers.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
    }

    public Result<IPaymentProvider> Resolve(string providerCode)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
        {
            return Result.Failure<IPaymentProvider>("PROVIDER_CODE_REQUIRED");
        }

        return _byCode.TryGetValue(providerCode.Trim(), out var provider)
            ? Result.Success(provider)
            : Result.Failure<IPaymentProvider>("PROVIDER_NOT_CONFIGURED");
    }
}
