using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace PayFlow.Multitenancy;

/// <summary>
/// Forwards the inbound caller's Bearer token onto outbound HttpClient
/// requests. Use on a typed HttpClient that calls another PayFlow service
/// in the request pipeline so the downstream service can verify the same
/// <c>tid</c> claim.
/// </summary>
public sealed class BearerTokenForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContext;

    public BearerTokenForwardingHandler(IHttpContextAccessor httpContext)
        => _httpContext = httpContext;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var inbound = _httpContext.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString();
        if (!string.IsNullOrWhiteSpace(inbound) && !request.Headers.Contains(HeaderNames.Authorization))
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, inbound);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
