# Event Catalog

Every integration event published to Kafka. Each consuming service re-declares the records it cares about in its own Application layer (see [Bounded contexts — Shared kernel](../architecture/bounded-contexts.md#shared-kernel) for why there is no centralised `Contracts` package); this file is the human-readable cross-reference and the source of truth for schema evolution.

Naming convention: `payflow.<context>.<event-name>.v<n>`. The major version is in the name; backwards-compatible additions do not change it.

Every event carries the standard headers:

- `message_id` — UUID, used by consumers for deduplication.
- `correlation_id` — propagated trace id (W3C `traceparent`).
- `causation_id` — id of the message or request that caused this one.
- `tenant_id` — denormalised into the header for fast filtering at the broker level.
- `schema_version` — major version, redundant with the event name but useful for non-strict consumers.

The "Consumers" column lists every service that subscribes to the event. New consumers add themselves here in the same PR that subscribes.

---

## Transaction events

### `payflow.transaction.initiated.v1`

Fired when a Transaction aggregate is created. Reporting uses this to count attempted volume; Notification ignores it.

| Field | Type | Notes |
|---|---|---|
| `transaction_id` | UUID | |
| `tenant_id` | UUID | |
| `order_reference` | string | merchant-supplied |
| `amount_minor` | int64 | |
| `currency` | string | ISO 4217 |
| `card_token_fingerprint` | string | for analytics; never the token itself |
| `metadata` | object | merchant pass-through |
| `at` | timestamp | |

**Consumers:** Reporting.

**Sample:**
```json
{
  "transaction_id": "f8b3c9d2-1e4a-4b7c-8d0e-2f1a5c6b7e8f",
  "tenant_id": "a1b2c3d4-1234-5678-9abc-def012345678",
  "order_reference": "ORD-2026-04-1124",
  "amount_minor": 14990,
  "currency": "TRY",
  "card_token_fingerprint": "f8b3c9d2",
  "metadata": { "channel": "web" },
  "at": "2026-04-21T09:14:22.341Z"
}
```

### `payflow.transaction.authorized.v1`

Provider authorised the funds. May or may not be followed by a capture event, depending on the merchant's flow.

| Field | Type | Notes |
|---|---|---|
| `transaction_id` | UUID | |
| `tenant_id` | UUID | |
| `provider_code` | string | `iyzico` / `stripe` / `paypal` |
| `provider_reference` | string | external id |
| `amount_minor` | int64 | |
| `currency` | string | |
| `attempt_number` | int | 1 if no failover happened |
| `at` | timestamp | |

**Consumers:** Reporting, Notification.

### `payflow.transaction.captured.v1`

Funds are now ours. The settlement event is separate and comes from Reconciliation later.

Schema identical to `authorized.v1` with `at` reflecting capture time.

**Consumers:** Reporting, Notification, Reconciliation (for next-day matching), Webhooks (fan-out to merchant subscriptions).

### `payflow.transaction.failed.v1`

| Field | Type | Notes |
|---|---|---|
| `transaction_id` | UUID | |
| `tenant_id` | UUID | |
| `failure_reason` | string | enum: `RoutingExhausted` / `HardDeclined` / `ProviderError` / `InvalidRequest` |
| `attempts` | array of object | per-attempt provider + decline reason |
| `at` | timestamp | |

**Consumers:** Reporting, Notification.

### `payflow.transaction.voided.v1`

Authorisation released before capture. Rare in normal flows.

**Consumers:** Reporting.

---

## Refund events

### `payflow.refund.requested.v1`

Started by Transaction service when a merchant requests a refund.

| Field | Type | Notes |
|---|---|---|
| `refund_id` | UUID | |
| `transaction_id` | UUID | the parent transaction |
| `tenant_id` | UUID | |
| `amount_minor` | int64 | <= captured amount |
| `currency` | string | |
| `requested_by` | string | `user:<id>` or `apikey:<id>` |
| `at` | timestamp | |

**Consumers:** Reconciliation (saga coordinator).

### `payflow.refund.processing.v1`

Published by Reconciliation once it has accepted a `RefundRequested` and started the provider call. Transaction consumes it to flip the local `refunds` row from `Requested` to `Processing` so dashboards reflect progress without polling.

| Field | Type | Notes |
|---|---|---|
| `refund_id` | UUID | |
| `transaction_id` | UUID | |
| `tenant_id` | UUID | |
| `saga_id` | string | for cross-context tracing |
| `at` | timestamp | |

**Consumers:** Transaction, Reporting.

### `payflow.refund.completed.v1`

Reconciliation publishes after the provider-side refund succeeds.

**Consumers:** Transaction (state transition), Reporting, Notification, Webhooks.

### `payflow.refund.failed.v1`

| Field | Type | Notes |
|---|---|---|
| `refund_id` | UUID | |
| `transaction_id` | UUID | |
| `tenant_id` | UUID | |
| `failure_reason` | string | provider-normalised |
| `at` | timestamp | |

**Consumers:** Transaction, Notification, Webhooks.

---

## Payment events

### `payflow.payment.completed.v1`

Payment service publishes per attempt, after it has produced a final status. Reporting uses these for per-provider success rate.

| Field | Type | Notes |
|---|---|---|
| `payment_id` | UUID | |
| `transaction_id` | UUID | for joining in projections |
| `tenant_id` | UUID | |
| `provider_code` | string | |
| `status` | string | enum: `Authorized` / `Captured` / `SoftDeclined` / `HardDeclined` / `ProviderUnavailable` |
| `latency_ms` | int | |
| `at` | timestamp | |

**Consumers:** Reporting.

---

## Reconciliation events

### `payflow.reconciliation.completed.v1`

| Field | Type | Notes |
|---|---|---|
| `run_id` | UUID | |
| `tenant_id` | UUID | |
| `provider_code` | string | |
| `settlement_date` | date | |
| `matched_count` | int | |
| `mismatch_count` | int | |
| `at` | timestamp | |

**Consumers:** Reporting.

### `payflow.reconciliation.mismatch_detected.v1`

One per mismatch, severity-tagged.

| Field | Type | Notes |
|---|---|---|
| `mismatch_id` | UUID | |
| `tenant_id` | UUID | |
| `kind` | string | enum |
| `severity` | string | `Low` / `Medium` / `High` |
| `transaction_id` | UUID | nullable |
| `statement_row_id` | UUID | nullable |
| `at` | timestamp | |

**Consumers:** Notification (high severity only).

---

## Identity events

### `payflow.identity.user_created.v1`

| Field | Type | Notes |
|---|---|---|
| `user_id` | UUID | |
| `tenant_id` | UUID | |
| `display_name` | string | |
| `at` | timestamp | |

**Consumers:** AI Assistant (`users_ref` projection).

### `payflow.identity.user_display_name_changed.v1`

| Field | Type | Notes |
|---|---|---|
| `user_id` | UUID | |
| `tenant_id` | UUID | |
| `display_name` | string | new value |
| `at` | timestamp | |

**Consumers:** AI Assistant.

### `payflow.identity.api_key_revoked.v1`

| Field | Type | Notes |
|---|---|---|
| `api_key_id` | UUID | |
| `tenant_id` | UUID | |
| `at` | timestamp | |

**Consumers:** Gateway (cache invalidation).

---

## Versioning rules

- Adding a new optional field is **non-breaking**, stays on `v1`. Existing consumers ignore unknown fields.
- Removing a field, changing its type, or making an optional field required is **breaking**, bumps to `v2`, lives on a new topic for the duration of the rollout, and the old version is deprecated with a removal date.
- A consumer that requires the new field must announce its dependency in this catalog (and in the producing service's release notes).

The deprecation lifecycle: announce in this file with a sunset date, ship the new version, run both in parallel, retire the old after every consumer has migrated.
