# Webhooks — Specification

Merchant-facing contract for outbound webhooks: what we hand to an integrator. Mirrors what the **PayFlow.Webhooks** service actually does today; the [Planned hardening](#planned-hardening) section at the bottom lists items the spec previously promised that the current implementation does not yet do.

## Subscribing

A subscription is *(tenant, event_type, url)*. The same merchant can register multiple URLs for the same event, or the same URL for multiple events — each registration is one subscription.

`POST /api/webhooks/subscriptions`

```json
{
  "eventType": "payflow.transaction.captured.v1",
  "url": "https://your-domain.example/payflow/webhook"
}
```

Response (`201 Created`):

```json
{
  "id": "f8b3c9d2-1e4a-4b7c-8d0e-2f1a5c6b7e8f",
  "eventType": "payflow.transaction.captured.v1",
  "url": "https://your-domain.example/payflow/webhook",
  "secret": "S3cR3t-base64-of-32-random-bytes-aGVsbG8="
}
```

The `secret` is returned **exactly once** — `GET /api/webhooks/subscriptions` omits it. If you lose it, deactivate the subscription and create a new one.

### Listing & deactivating

```
GET    /api/webhooks/subscriptions          — list subscriptions for the caller's tenant
DELETE /api/webhooks/subscriptions/{id}     — deactivate (the row is kept, audit-trail style)
```

### Inspecting deliveries

```
GET /api/webhooks/deliveries?take=100
```

Returns the latest N delivery rows for the tenant — id, subscription id, event type, target URL, state (`Pending` / `Sent` / `Failed`), attempt count, last status code, last error, timestamps. Use this to debug an integration: a failing endpoint surfaces as repeated `Pending` rows with `next_attempt_at` advancing, and a malformed endpoint surfaces as a `Failed` row with a 4xx status.

## Payload format

The HTTP body is the event payload itself — there is no outer envelope. The dispatcher emits one of the shapes below per `event_type`. The keys are camelCase JSON.

### `payflow.transaction.captured.v1`

```json
{
  "id": "8e1a44bc-90f3-4ec0-8c11-3d8c7e4ab219",
  "eventType": "payflow.transaction.captured.v1",
  "occurredAt": "2026-05-21T09:14:22.341+00:00",
  "data": {
    "transactionId": "f8b3c9d2-1e4a-4b7c-8d0e-2f1a5c6b7e8f",
    "providerCode": "stripe",
    "providerReference": "ch_3MzUKZ...",
    "amountMinor": 14990,
    "currency": "TRY"
  }
}
```

### `payflow.refund.completed.v1`

```json
{
  "id": "0e7c...",
  "eventType": "payflow.refund.completed.v1",
  "occurredAt": "2026-05-21T09:14:25.114+00:00",
  "data": {
    "refundId": "...",
    "transactionId": "...",
    "amountMinor": 3000,
    "currency": "TRY",
    "providerCode": "stripe",
    "providerReference": "re_3MzUKZ..."
  }
}
```

### `payflow.refund.failed.v1`

```json
{
  "id": "...",
  "eventType": "payflow.refund.failed.v1",
  "occurredAt": "...",
  "data": {
    "refundId": "...",
    "transactionId": "...",
    "failureReason": "INSUFFICIENT_FUNDS"
  }
}
```

The top-level `id` is the **source event id** (Kafka `MessageId`) — the same id appears on every redelivery and on every subscription that fans out from this event, so it's the right key for merchant-side deduplication. The full event catalogue with payload schemas is at [docs/events/catalog.md](../events/catalog.md).

## Headers

```
Content-Type: application/json
User-Agent: PayFlow-Webhooks/1.0
X-PayFlow-Signature: sha256=<lowercase hex>
```

## Verifying the signature

`X-PayFlow-Signature` is `sha256=<hex-digest>` where the digest is `HMAC-SHA256(secret, raw_request_body)`. UTF-8 bytes, lowercase hex. The same shape Stripe / Shopify use.

C# example:

```csharp
public static bool Verify(string body, string signatureHeader, string secret)
{
    const string Prefix = "sha256=";
    if (!signatureHeader.StartsWith(Prefix, StringComparison.Ordinal)) return false;

    var received = signatureHeader[Prefix.Length..];
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)))
        .ToLowerInvariant();

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected),
        Encoding.UTF8.GetBytes(received));
}
```

Use the raw body bytes — not a re-serialised JSON. Any whitespace or ordering change between what we sent and what your verifier reconstructs will break the signature.

## Acknowledging

Respond with any 2xx within 10 seconds (the dispatcher's per-call HTTP timeout) and we consider the delivery successful. If your processing takes longer than 10 seconds, acknowledge immediately and continue asynchronously.

Non-2xx responses are categorised:

- **`408`, `429`, `5xx`, network / TLS / DNS error, timeout:** retried per the schedule below.
- **Other `4xx` (`400`, `401`, `403`, `404`, `410`, `422`, …):** treated as terminal. The delivery moves to `Failed` immediately. We assume the receiver is rejecting on purpose (bad signature, unsupported event, endpoint removed) and that retrying won't help.

## Retry schedule

Five attempts total. After the fifth failure the delivery becomes `Failed` (terminal).

| Attempt | Delay from previous |
|---|---|
| 1 → 2 | 30 seconds |
| 2 → 3 | 2 minutes |
| 3 → 4 | 10 minutes |
| 4 → 5 | 30 minutes |
| 5 → terminal | — |

A background sweeper inside the Webhooks service polls every 30 seconds for `Pending` rows whose `next_attempt_at` has elapsed and re-drives them through the same dispatch path. The retry budget is per-delivery, not per-subscription.

## Idempotency on your side

The same `id` (top-level — the Kafka message id) can arrive more than once if the merchant endpoint successfully processed an earlier attempt but failed to ack within 10s. Dedupe on `id` and treat the first delivery as authoritative.

The dispatcher itself dedupes on `(subscription_id, source_message_id)` via a unique index, so the *same source event* never produces two delivery rows against the same subscription even if the upstream Kafka message is replayed by a consumer-group reset.

## Operational notes

- We do not follow redirects. The endpoint URL you register is the one we hit.
- The per-call HTTP timeout is 10 seconds. Endpoints slower than that count as transient failures.
- `User-Agent` is fixed at `PayFlow-Webhooks/1.0`.
- Subscription `Url` validation accepts `http://` and `https://`; production deployments should add a policy gate that requires HTTPS (see Planned hardening).

## Planned hardening

The current implementation is the smallest thing that demonstrates the pattern. Items that the previous spec promised and that production-grade deployments would want:

- **Timestamp-prefixed signature + replay window.** The Stripe-style `t=<unix>,v1=<hex>` form makes the signature cover `(timestamp, body)` and lets receivers reject deliveries older than ~5 minutes. Today the signature covers the body only.
- **TLS-only enforcement.** Reject `http://` URLs at the API layer in production; today the domain validator accepts both.
- **Auto-disable on prolonged failure.** Currently a permanently-broken endpoint stays in `Active` and keeps accumulating `Failed` deliveries. The spec previously promised auto-disable after 7 days of consecutive failures with an email to the tenant's admin.
- **Replay from dashboard.** Re-issue a previously-failed delivery from the audit row. Today you'd POST a manual repeat.
- **Longer retry tail.** Five attempts over ~45 minutes is enough for transient blips, not enough for an endpoint that's down for hours. A ~24h tail with diminishing-returns backoff (e.g. 2h / 6h / 12h / 24h) is typical for payment webhooks.

Each of these is a small change against the existing service; none requires schema or contract changes.
