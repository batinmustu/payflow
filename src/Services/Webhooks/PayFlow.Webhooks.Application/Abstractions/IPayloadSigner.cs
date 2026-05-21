namespace PayFlow.Webhooks.Application.Abstractions;

/// <summary>
/// HMAC-SHA256 signer for outgoing webhook payloads. Returns the value
/// to send in the <c>X-PayFlow-Signature</c> header (format
/// <c>sha256=&lt;hex&gt;</c>). Kept in Application so the Dispatcher can
/// stay infrastructure-free.
/// </summary>
public interface IPayloadSigner
{
    string Sign(string payload, string secret);
}
