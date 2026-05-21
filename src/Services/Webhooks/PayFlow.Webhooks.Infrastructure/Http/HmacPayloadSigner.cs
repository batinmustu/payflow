using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PayFlow.Webhooks.Application.Abstractions;

namespace PayFlow.Webhooks.Infrastructure.Http;

/// <summary>
/// HMAC-SHA256 signer. Header value format <c>sha256=&lt;hex&gt;</c> —
/// same shape Stripe / GitHub / Shopify use, so merchants can reuse
/// existing verification snippets.
/// </summary>
internal sealed class HmacPayloadSigner : IPayloadSigner
{
    public string Sign(string payload, string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);
        ArgumentException.ThrowIfNullOrEmpty(secret);

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(bodyBytes);
        return "sha256=" + Convert.ToHexString(hash).ToLower(CultureInfo.InvariantCulture);
    }
}
