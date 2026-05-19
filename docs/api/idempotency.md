# Idempotency Guide

State-creating endpoints in PayFlow require an `Idempotency-Key` header. This guide is for integrators.

## Why we require it

A transaction-create request can fail in a way where you don't know whether we processed it. Network reset, timeout, ALB blip, the usual suspects. Without idempotency, your only safe choice is "wait and query" — which is awkward at best and creates phantom transactions at worst.

The `Idempotency-Key` header lets you retry exactly. If the original succeeded, the retry returns the original response. If the original failed cleanly, the retry produces a new attempt. If the original is still in flight, you get a `409` telling you to wait.

## Endpoints that require it

| Endpoint | Method |
|---|---|
| `/api/transactions` | `POST` |
| `/api/transactions/{id}/captures` | `POST` |
| `/api/transactions/{id}/voids` | `POST` |
| `/api/transactions/{id}/refunds` | `POST` |
| `/api/webhooks` | `POST` |

Read endpoints (`GET`) and updates that are themselves naturally idempotent (`PUT`, `DELETE`) do not need the header.

## How to generate the key

Anything you can guarantee unique per logical operation. Most clients use a UUIDv4. Some hash the order id with a salt. We do not care what you pick as long as you do not reuse the same key for a different logical operation.

Rules:

- ≤ 64 ASCII characters.
- Unique within `(your tenant, the endpoint, 24 hours)`. Two tenants using the same string never collide. Two endpoints using the same string never collide. The same string used 25 hours apart never collides — but you should not rely on that.

## How retries work

When you send a request with `Idempotency-Key: <key>`:

1. We hash the request body (SHA-256).
2. We look up `(tenant_id, request_path, key)` in our store.
3. If absent: we proceed normally and remember the response.
4. If present and we already have a stored response: we return that response, with header `Idempotency-Replayed: true`.
5. If present but the original request is still in flight: we return `409 IDEMPOTENCY_KEY_IN_PROGRESS`. Wait and retry; do not change the body.
6. If present but with a different body hash: we return `422 IDEMPOTENCY_KEY_REUSED_DIFFERENT_BODY`. This is a client bug.

## What we store

The stored response is the *complete* response: status, headers, body. A replay returns exactly the same bytes. This matters: a client doing fingerprint-based dedup on its end can trust the response byte-for-byte.

We do not store the request body, only its hash. We have no need to read your body again, and not storing it limits the blast radius if our store is compromised.

## TTL

24 hours after the first time we saw the key. After that, the key is forgotten and a new request with the same key is a new request.

The 24-hour window is the right size for the failure modes idempotency addresses (immediate retries on transient failures, plus some hours of backoff). A retry sent a day later is almost certainly a different intent.

## What counts as "same body"

The hash is over the canonical bytes you sent us — not a re-serialised version. If you send:

```json
{"amount_minor":14990,"currency":"TRY"}
```

and then retry with:

```json
{"currency": "TRY", "amount_minor": 14990}
```

…those are different bodies as far as we are concerned. They serialise to different bytes. Don't whitespace-format your requests differently between retries.

## Example

First attempt:

```
POST /api/transactions
Authorization: Bearer pk_live_...
Idempotency-Key: 9f8c7b6a-5d4e-3c2b-1a09-8b7c6d5e4f3a
Content-Type: application/json

{"order_reference":"ORD-2026-04-1124","amount_minor":14990,"currency":"TRY","card_token":"tok_..."}
```

Response (success):

```
HTTP/1.1 200 OK
Content-Type: application/json
Idempotency-Key: 9f8c7b6a-5d4e-3c2b-1a09-8b7c6d5e4f3a

{"transaction_id":"txn_...","status":"Captured", ...}
```

A second identical request returns the same body and status, plus `Idempotency-Replayed: true`.

## What we recommend on your side

- **Generate the key before you send.** Don't generate it inside a retry loop. The whole point is to reuse the same key on retries.
- **Persist the key with your order.** If your service restarts mid-flight, the new process should pick up the same key when it retries.
- **Treat `409 IDEMPOTENCY_KEY_IN_PROGRESS` as "wait and retry the exact same request"** with a short backoff. Don't generate a new key.
- **Treat `422 IDEMPOTENCY_KEY_REUSED_DIFFERENT_BODY` as a programming bug.** It means two different operations got the same key by accident, which is a problem in your code, not ours.

## What we don't do

- **We don't auto-retry on your behalf.** PayFlow does not retry failed requests for you. The header is for *your* retries.
- **We don't extend the TTL on access.** Once seen, the 24 hours start. Looking up the key (via a retry) does not refresh the timer.
- **We don't share keys across tenants.** Two tenants can use the same string. They'll never see each other's results.

## When our idempotency store is unavailable

If the Redis-backed store is unreachable (cluster down, network partition, timeout), we **fail closed**:

- The response is `503 SERVICE_UNAVAILABLE` with `code: IDEMPOTENCY_STORE_UNAVAILABLE`.
- No business work happens. We never insert a transaction without first reserving the key.
- `Retry-After` is set; treat it as any other 5xx and back off.

The reason: with the store down we cannot guarantee dedup. The cost of failing closed is occasional unavailability during a Redis incident. The cost of failing open is double-charged customers. We pick the first.

This is reflected in the [threat model](../security/threat-model.md) under "Idempotency-key bypass for retries".

## Edge cases we picked a side on

- **Request with key but empty body.** Allowed. Empty body has a defined hash.
- **Request with key on a `GET`.** Ignored. We do not enforce idempotency on read-only operations; the header travels through but does nothing.
- **Same key, two near-simultaneous identical requests.** One wins the `SETNX`-style write to the store and proceeds. The other gets `IDEMPOTENCY_KEY_IN_PROGRESS` and should retry. After the winner finishes, retries succeed and return the stored response.
- **Idempotency on success, also on failure.** Failures are stored too. A retry of a request that resulted in a `422 VALIDATION_ERROR` returns the same `422` with the same body. This is intentional — your client should not get different errors on retry.
