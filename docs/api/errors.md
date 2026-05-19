# Error Code Reference

Every error returned from the PayFlow API follows the same shape:

```json
{
  "error": {
    "code": "PROVIDER_DECLINED",
    "message": "The provider declined this charge.",
    "details": { "decline_code": "insufficient_funds" },
    "trace_id": "01HXY..."
  }
}
```

- `code` — a stable machine-readable identifier. We do not change codes. If a code's meaning evolves, we introduce a new one and deprecate the old.
- `message` — a human-readable one-liner. Localised in the future; currently English.
- `details` — optional structured fields specific to the error. Schema varies by code; never assume a key is present.
- `trace_id` — the W3C trace id for this request. Quote it when contacting support.

The HTTP status corresponds to the class of error: `400` for client mistakes, `401`/`403` for auth, `404` for resource lookup, `409` for conflict, `422` for validation, `429` for rate limit, `5xx` for server-side issues. The `code` disambiguates within the class.

## Authentication & authorisation (`401`, `403`)

| Code | HTTP | Meaning |
|---|---|---|
| `AUTH_MISSING_TOKEN` | 401 | No `Authorization` header. |
| `AUTH_INVALID_TOKEN` | 401 | Token is malformed, has a bad signature, or fails issuer/audience checks. |
| `AUTH_TOKEN_EXPIRED` | 401 | The token was valid but its `exp` is past. Refresh and retry. |
| `AUTH_API_KEY_REVOKED` | 401 | API key was revoked by the tenant. Generate a new one. |
| `AUTH_SESSION_EXPIRED` | 401 | Refresh token is expired or unknown. Log in again. |
| `AUTH_SESSION_COMPROMISED` | 401 | A refresh-token reuse was detected; the session family was revoked. Log in again. |
| `AUTH_FORBIDDEN` | 403 | Authenticated but lacking the required role/permission for this operation. |
| `AUTH_TENANT_MISMATCH` | 403 | The resource belongs to a different tenant. |

## Validation (`400`, `422`)

| Code | HTTP | Meaning |
|---|---|---|
| `VALIDATION_ERROR` | 422 | One or more fields failed validation. `details.errors` is an array of `{ field, code, message }`. |
| `INVALID_REQUEST_BODY` | 400 | Body could not be parsed as JSON. |
| `UNSUPPORTED_CONTENT_TYPE` | 400 | `Content-Type` is not `application/json`. |
| `UNKNOWN_FIELD` | 400 | A field in the body is not allowed for this endpoint. We are strict about unknown fields for safety. |

Example `VALIDATION_ERROR` details:

```json
{
  "details": {
    "errors": [
      { "field": "amount_minor", "code": "must_be_positive", "message": "Amount must be greater than zero." },
      { "field": "currency", "code": "iso_4217_required", "message": "Currency must be a 3-letter ISO 4217 code." }
    ]
  }
}
```

## Idempotency (`400`, `409`, `422`, `503`)

| Code | HTTP | Meaning |
|---|---|---|
| `IDEMPOTENCY_KEY_REQUIRED` | 400 | This endpoint requires an `Idempotency-Key` header. |
| `IDEMPOTENCY_KEY_TOO_LONG` | 400 | Maximum length is 64 characters. |
| `IDEMPOTENCY_KEY_IN_PROGRESS` | 409 | The same key is currently being processed. Wait and retry, or query the previous response if it has completed. |
| `IDEMPOTENCY_KEY_REUSED_DIFFERENT_BODY` | 422 | The same key was used with a different request body. Either the original or this retry is wrong. |
| `IDEMPOTENCY_STORE_UNAVAILABLE` | 503 | The idempotency store is unreachable; we fail closed rather than risk a duplicate. `Retry-After` is set. |

Full guide: [docs/api/idempotency.md](idempotency.md).

## Transaction lifecycle (`400`, `404`, `409`, `422`)

| Code | HTTP | Meaning |
|---|---|---|
| `TRANSACTION_NOT_FOUND` | 404 | No transaction with that id in this tenant. |
| `TRANSACTION_INVALID_STATE` | 409 | The operation is not allowed in the transaction's current state (e.g. capture on a `Failed` transaction). |
| `ORDER_REFERENCE_ALREADY_USED` | 409 | The `order_reference` already exists for this tenant. Each transaction must use a unique reference per tenant. |
| `AMOUNT_INVALID` | 422 | Amount is zero, negative, or exceeds the tenant's per-transaction ceiling. |
| `CURRENCY_NOT_SUPPORTED` | 422 | The currency is not enabled for this tenant. |
| `CARD_TOKEN_INVALID` | 422 | The supplied card token is malformed or unknown to the configured provider. |

## Routing & provider (`422`, `502`, `503`)

| Code | HTTP | Meaning |
|---|---|---|
| `ROUTING_NO_PROVIDER_CONFIGURED` | 422 | The tenant has no active routing rule. Configure providers first. |
| `ROUTING_EXHAUSTED` | 422 | Every provider in the rule attempted; all returned soft declines or hit daily limits. `details.attempts` lists per-attempt reasons. |
| `PROVIDER_HARD_DECLINED` | 422 | A provider returned a hard decline. No further providers attempted by design. `details.decline_code` is the normalised reason. |
| `PROVIDER_UNAVAILABLE` | 502 | A provider could not be reached and no fallback succeeded. |
| `PROVIDER_TIMEOUT` | 504 | The entire request exceeded the 30s wall-clock budget. The transaction state is unknown — a reconciliation row will appear if the provider eventually settled it. |

## Refund (`404`, `409`, `422`)

| Code | HTTP | Meaning |
|---|---|---|
| `REFUND_NOT_ALLOWED_IN_STATE` | 409 | The transaction is not in a refundable state. |
| `REFUND_AMOUNT_EXCEEDS_REMAINING` | 422 | The requested amount is greater than `captured_amount - sum(prior_refunds)`. |
| `REFUND_NOT_FOUND` | 404 | No refund with that id in this tenant. |
| `REFUND_WINDOW_EXPIRED` | 422 | The provider's refund window for this transaction has passed (typically 180 days). |

## Rate limiting (`429`)

| Code | HTTP | Meaning |
|---|---|---|
| `RATE_LIMITED` | 429 | The tenant exceeded the per-endpoint rate limit. `Retry-After` header tells you when to retry. |

The per-endpoint limits are documented in the integration guide; the headers are always present on rate-limited responses (`X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`).

## Server errors (`500`, `503`)

| Code | HTTP | Meaning |
|---|---|---|
| `INTERNAL_ERROR` | 500 | We did something wrong. Trace id in the response body; please report. |
| `SERVICE_UNAVAILABLE` | 503 | A required downstream is down. Retry with backoff. |
| `MAINTENANCE` | 503 | Planned maintenance window. `Retry-After` is set. |

## Conventions

- **Codes are SCREAMING_SNAKE_CASE** and stable.
- **`details`** is optional; never assume a key.
- **`message`** is for humans, not for branching logic. Branch on `code`.
- **`trace_id`** is always present on errors. It is the same value we record server-side and lets support find the request in seconds.

## Adding a new code

1. Add it to this file with a clear meaning and HTTP status.
2. Add it to the `ErrorCodes` static class in `PayFlow.SharedKernel`.
3. Ensure the code maps to an HTTP status in the global exception handler.
4. If it's a client-facing meaningful error, mention it in the relevant flow doc.
