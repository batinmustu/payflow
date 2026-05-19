using PayFlow.Payment.Domain;
using PayFlow.SharedKernel;

namespace PayFlow.Payment.Application.Abstractions;

/// <summary>
/// Selects a registered <see cref="IPaymentProvider"/> by code at request
/// time. Today the selection is "match code to adapter"; the factory exists
/// so future routing rules (per-tenant enablement, fallback ordering) land
/// in one place rather than being scattered through callers.
/// </summary>
public interface IPaymentProviderFactory
{
    Result<IPaymentProvider> Resolve(string providerCode);
}
