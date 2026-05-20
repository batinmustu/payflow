using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace PayFlow.Transaction.API.Idempotency;

/// <summary>
/// Enforces the contract in docs/api/idempotency.md on POST /api/transactions:
///   - Missing or oversized key → 400 (per the spec)
///   - First time seen → reserve, run the handler, store the response
///   - Same key + same body still running → 409
///   - Same key + same body completed → replay verbatim (Idempotency-Replayed: true)
///   - Same key + different body completed → 422
///   - Store unreachable → 503 (fail closed)
///
/// Auth runs before this middleware so the tenant id is available from
/// the principal's `tid` claim. Anonymous requests skip the middleware
/// entirely and let the endpoint's RequireAuthorization() return 401.
/// </summary>
internal sealed class IdempotencyMiddleware
{
    private const string HeaderName = "Idempotency-Key";
    private const string ReplayedHeader = "Idempotency-Replayed";
    private const int MaxKeyLength = 64;

    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        if (!AppliesTo(context.Request))
        {
            await _next(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            // Unauthenticated — let RequireAuthorization() on the endpoint
            // return 401. We do not enforce idempotency on a request that
            // is going to be rejected anyway.
            await _next(context);
            return;
        }

        var tenantClaim = context.User.FindFirst("tid")?.Value;
        if (!Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues.ToString()))
        {
            await WriteProblem(context,
                StatusCodes.Status400BadRequest,
                "IDEMPOTENCY_KEY_REQUIRED",
                "Idempotency-Key header is required on this endpoint.");
            return;
        }

        var clientKey = keyValues.ToString().Trim();
        if (clientKey.Length > MaxKeyLength)
        {
            await WriteProblem(context,
                StatusCodes.Status400BadRequest,
                "IDEMPOTENCY_KEY_TOO_LONG",
                $"Idempotency-Key must be no longer than {MaxKeyLength} ASCII characters.");
            return;
        }

        var bodyHash = await HashRequestBodyAsync(context.Request, context.RequestAborted);
        var idemKey = new IdempotencyKey(tenantId, context.Request.Path.Value!, clientKey);

        IdempotencyReservation reservation;
        try
        {
            reservation = await store.TryReserveAsync(idemKey, bodyHash, context.RequestAborted);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or RedisException)
        {
            _logger.LogError(ex, "Idempotency store unavailable for key {Key}", clientKey);
            await WriteProblem(context,
                StatusCodes.Status503ServiceUnavailable,
                "IDEMPOTENCY_STORE_UNAVAILABLE",
                "The idempotency store is unreachable; please retry.");
            context.Response.Headers.RetryAfter = "5";
            return;
        }

        switch (reservation.Outcome)
        {
            case ReservationOutcome.Reserved:
                await RunAndStoreAsync(context, idemKey, bodyHash, store);
                return;

            case ReservationOutcome.InFlight:
                await WriteProblem(context,
                    StatusCodes.Status409Conflict,
                    "IDEMPOTENCY_KEY_IN_PROGRESS",
                    "The same Idempotency-Key is currently being processed.");
                return;

            case ReservationOutcome.CompletedSameBody:
                await ReplayAsync(context, reservation.Replay!);
                return;

            case ReservationOutcome.CompletedDifferentBody:
                await WriteProblem(context,
                    StatusCodes.Status422UnprocessableEntity,
                    "IDEMPOTENCY_KEY_REUSED_DIFFERENT_BODY",
                    "Same Idempotency-Key reused with a different request body.");
                return;

            default:
                throw new InvalidOperationException($"Unknown reservation outcome: {reservation.Outcome}");
        }
    }

    private static bool AppliesTo(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        && request.Path.StartsWithSegments("/api/transactions");

    private static async Task<string> HashRequestBodyAsync(HttpRequest request, CancellationToken ct)
    {
        request.EnableBuffering();
        request.Body.Position = 0;
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct);
        request.Body.Position = 0;

        var hash = SHA256.HashData(buffer.ToArray());
        return Convert.ToHexString(hash);
    }

    private async Task RunAndStoreAsync(
        HttpContext context,
        IdempotencyKey key,
        string bodyHash,
        IIdempotencyStore store)
    {
        var originalBody = context.Response.Body;
        await using var capturedBody = new MemoryStream();
        context.Response.Body = capturedBody;

        try
        {
            await _next(context);
        }
        finally
        {
            capturedBody.Position = 0;
            await capturedBody.CopyToAsync(originalBody, context.RequestAborted);
            context.Response.Body = originalBody;
        }

        // Capture for replay. Read what the handler wrote.
        capturedBody.Position = 0;
        var bodyText = await new StreamReader(capturedBody, Encoding.UTF8).ReadToEndAsync(context.RequestAborted);
        var stored = new StoredResponse(
            StatusCode: context.Response.StatusCode,
            ContentType: context.Response.ContentType ?? "application/octet-stream",
            Body: bodyText);

        try
        {
            await store.CompleteAsync(key, bodyHash, stored, context.RequestAborted);
        }
        catch (Exception ex)
        {
            // Don't fail the client because the store hiccupped on the way out —
            // the work is already done. Worst case the next retry runs the
            // command again (the application enforces unique order_reference).
            _logger.LogWarning(ex, "Failed to persist idempotency record for key {Key}", key.ClientKey);
        }
    }

    private static async Task ReplayAsync(HttpContext context, StoredResponse stored)
    {
        context.Response.StatusCode = stored.StatusCode;
        context.Response.ContentType = stored.ContentType;
        context.Response.Headers[ReplayedHeader] = "true";
        await context.Response.WriteAsync(stored.Body, context.RequestAborted);
    }

    private static async Task WriteProblem(HttpContext context, int statusCode, string code, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = new
        {
            type = $"https://datatracker.ietf.org/doc/html/rfc9457",
            title = code.Replace('_', ' ').ToLowerInvariant(),
            status = statusCode,
            detail,
            code,
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem), context.RequestAborted);
    }
}
