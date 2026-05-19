# Webhooks — Specification

This is the merchant-facing contract for outbound webhooks — what we hand to an integrator.

## Subscribing

`POST /api/webhooks`

```json
{
  "url": "https://your-domain.example/payflow/webhook",
  "events": [
    "payflow.transaction.captured.v1",
    "payflow.transaction.failed.v1",
    "payflow.refund.completed.v1"
  ],
  "description": "Production"
}
```

Response:

```json
{
  "id": "we_8f3a...",
  "url": "...",
  "events": [...],
  "secret": "whsec_<54 random chars>",
  "status": "Active",
  "created_at": "..."
}
```

The `secret` is returned exactly once. Store it in your secrets store immediately. If you lose it, generate a new one (which rotates with a grace window so existing deliveries are not interrupted).

## Payload format

Every delivery has this envelope:

```json
{
  "id": "wd_8f3a...",
  "event_id": "msg_b71f9c...",
  "event_type": "payflow.transaction.captured.v1",
  "tenant_id": "ten_...",
  "produced_at": "2026-04-21T09:14:22.341Z",
  "delivered_at": "2026-04-21T09:14:22.842Z",
  "data": { /* event-specific payload, see below */ }
}
```

- `id` — unique per delivery attempt. Useful for tracing in your logs.
- `event_id` — unique per *event*. Use this to dedupe; redelivered events share the same id.
- `event_type` — the topic name; informs the `data` shape.
- `produced_at` — when the event was produced upstream. Approximately when the business state change happened.
- `delivered_at` — when we sent this delivery attempt.
- `data` — the event payload. Schema per event type below.

## Supported event types

| Event type | When |
|---|---|
| `payflow.transaction.captured.v1` | A transaction reached `Captured`. The most common subscription. |
| `payflow.transaction.authorized.v1` | A transaction reached `Authorized` (auth-only flow). |
| `payflow.transaction.failed.v1` | A transaction reached `Failed`. |
| `payflow.transaction.voided.v1` | A previously authorised transaction was voided. |
| `payflow.refund.completed.v1` | A refund reached `Completed`. |
| `payflow.refund.failed.v1` | A refund reached `Failed`. |

The payloads inside `data` mirror the catalogue in [docs/events/catalog.md](../events/catalog.md). Example for `transaction.captured.v1`:

```json
{
  "transaction_id": "txn_f8b3c9d2...",
  "tenant_id": "ten_a1b2c3d4...",
  "order_reference": "ORD-2026-04-1124",
  "provider_code": "stripe",
  "provider_reference": "ch_3MzUKZ...",
  "amount_minor": 14990,
  "currency": "TRY",
  "attempt_number": 1,
  "at": "2026-04-21T09:14:22.341Z"
}
```

## Verifying the signature

Every request carries:

```
X-PayFlow-Signature: t=1714643662,v1=8f9d2a...
X-PayFlow-Event-Id: msg_b71f9c...
User-Agent: PayFlow-Webhook/1.0
Content-Type: application/json
```

To verify:

1. Parse `X-PayFlow-Signature` into `t` and `v1`.
2. Reject if `now - t > 300` seconds (replay protection).
3. Compute `expected = HMAC_SHA256(secret, f"{t}.{raw_request_body}")`.
4. Compare to `v1` in constant time. Reject if they don't match.

C# example:

```csharp
public static bool Verify(string body, string signatureHeader, string secret)
{
    var parts = signatureHeader.Split(',')
        .Select(p => p.Split('=', 2))
        .ToDictionary(p => p[0], p => p[1]);

    var t = long.Parse(parts["t"]);
    if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - t) > 300)
        return false;

    var signed = $"{t}.{body}";
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed)));
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected),
        Encoding.UTF8.GetBytes(parts["v1"].ToUpperInvariant()));
}
```

Use the raw body bytes — not a re-serialised JSON. Any whitespace or ordering change between what we sent and what your verifier reconstructs will break the signature.

## Acknowledging

Respond with any 2xx within 10 seconds and we consider the delivery successful. If your processing takes longer than 10 seconds, acknowledge immediately and continue asynchronously.

Non-2xx responses are categorised:

- **`408`, `429`, `5xx`, network error:** retried per the schedule below.
- **Other `4xx`:** treated as terminal. The delivery moves to `Failed` and goes to your DLQ-equivalent view. We assume the receiver is rejecting on purpose (e.g. bad signature, unsupported event, deactivated webhook).

## Retry schedule

| Attempt | Delay from previous |
|---|---|
| 1 → 2 | 30 seconds |
| 2 → 3 | 2 minutes |
| 3 → 4 | 10 minutes |
| 4 → 5 | 30 minutes |
| 5 → 6 | 2 hours |
| 6 → 7 | 6 hours |
| 7 → 8 | 12 hours |
| 8 → 9 | 24 hours |
| 9 | terminal — DLQ |

Total window ~45 hours. You see every attempt in the dashboard.

## Idempotency on your side

The same `X-PayFlow-Event-Id` can arrive more than once (rare; happens when an ack was lost). Dedupe on `event_id` and treat the first delivery as authoritative.

The same *transaction* may be the subject of multiple events (one captured, then one refunded later). Those have different `event_id`s but the same `data.transaction_id`. That is normal, not a duplicate.

## Replaying

From the dashboard you can replay a single delivery or every delivery for a given event. Replays use the same `event_id` and a new delivery `id`. You should idempotently ignore replays based on `event_id` (you have already processed it) or process them again if that suits your reconciliation model — both are fine.

## Operational notes

- We do not allow `http://` URLs. TLS is required.
- We do not follow redirects. The endpoint URL you register is the one we hit.
- A webhook endpoint that has failed every delivery for 7 consecutive days is automatically disabled, with an email to the tenant's admin. Re-enable from the dashboard after fixing the receiver.
- The `User-Agent` is fixed at `PayFlow-Webhook/1.0`. Use it for routing on your side if you share an ingress with other senders.
