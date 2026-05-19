# ERD — Transaction Service

The central context. Owns the Transaction aggregate, the outbox, routing rules, and refund records. Idempotency-key bookkeeping lives in Redis (the request middleware does the work) and is mirrored here only for audit.

```mermaid
erDiagram
    TRANSACTIONS ||--o{ TRANSACTION_STATE_HISTORY : transitions
    TRANSACTIONS ||--o{ REFUNDS : may_have
    TRANSACTIONS ||--o{ TRANSACTION_PAYMENT_ATTEMPTS : routes_to
    TRANSACTIONS ||--o{ IDEMPOTENCY_KEYS : protected_by
    ROUTING_RULES ||--o{ ROUTING_RULE_PROVIDERS : ordered_list
    TENANT_ROUTING_RULES ||--|| ROUTING_RULES : current

    TRANSACTIONS {
        uuid id PK
        uuid tenant_id FK
        text order_reference "merchant-supplied"
        bigint amount_minor "captured amount once Captured"
        bigint refunded_amount_minor "cumulative; 0 until first RefundCompleted"
        text currency
        text state "Initiated | Authorized | Captured | PartiallyRefunded | Refunded | Voided | Failed"
        text final_provider_code "set when reaches authorized/captured"
        text failure_reason "RoutingExhausted | HardDeclined | ProviderError | InvalidRequest"
        text card_token_fingerprint "for analytics, not the token itself"
        jsonb metadata "[PII] merchant pass-through, opaque"
        int row_version "optimistic concurrency"
        timestamptz created_at
        timestamptz authorized_at
        timestamptz captured_at
        timestamptz failed_at
    }

    TRANSACTION_STATE_HISTORY {
        bigint id PK
        uuid transaction_id FK
        text from_state
        text to_state
        text reason
        text actor "system | user | saga"
        timestamptz at
    }

    TRANSACTION_PAYMENT_ATTEMPTS {
        uuid id PK
        uuid transaction_id FK
        int attempt_number
        text provider_code
        uuid payment_id "FK across service boundary"
        text result "Authorized | Captured | SoftDeclined | HardDeclined | ProviderUnavailable"
        timestamptz at
    }

    REFUNDS {
        uuid id PK
        uuid transaction_id FK
        uuid tenant_id FK
        bigint amount_minor
        text status "Requested | Processing | Completed | Failed"
        text saga_id
        text failure_reason
        timestamptz requested_at
        timestamptz completed_at
    }

    ROUTING_RULES {
        uuid id PK
        uuid tenant_id FK
        text name
        bool is_active
        timestamptz created_at
    }

    ROUTING_RULE_PROVIDERS {
        uuid rule_id FK
        text provider_code
        int priority "1 = first attempt"
        bigint daily_limit_minor
    }

    TENANT_ROUTING_RULES {
        uuid tenant_id PK,FK
        uuid active_rule_id FK
    }

    IDEMPOTENCY_KEYS {
        uuid id PK
        uuid tenant_id FK
        text request_path
        text idempotency_key
        text request_body_hash
        uuid transaction_id "set on completion"
        text response_status
        timestamptz seen_at
        timestamptz completed_at
    }

    OUTBOX_MESSAGES {
        uuid id PK
        uuid tenant_id FK
        text aggregate_type "Transaction | Refund"
        uuid aggregate_id
        text event_type "payflow.transaction.captured.v1 | ..."
        jsonb payload
        jsonb headers
        text state "Pending | Publishing | Published | Failed"
        int attempt_count
        text last_error
        timestamptz created_at
        timestamptz published_at
        timestamptz next_attempt_at
    }
```

## Notes

- **The aggregate boundary.** `transactions` and `refunds` are separate aggregates that reference each other; refunds are not modelled as a collection on the transaction row. This makes refund processing independent of transaction loads — important because refund sagas can run long after the original transaction is otherwise dormant.
- **`refunded_amount_minor` is denormalised.** It tracks the cumulative refunded amount on the parent transaction so the state machine's `Captured → PartiallyRefunded → Refunded` decision is a single-row read. The authoritative ledger of refunds is the `refunds` table; the column is updated by the same handler that consumes `payflow.refund.completed.v1` (see [state-machines/transaction.md](../state-machines/transaction.md)). A reconciliation invariant asserts `refunded_amount_minor = sum(refunds where status = Completed)` per transaction.
- **`row_version`** powers optimistic concurrency for the aggregate. Two consumers reacting to refund events at the same time produce one winner and one retry, never a lost update.
- **`transaction_payment_attempts`** is the routing chain. A transaction that succeeded on the first try has one row; one that failed over has multiple. The `payment_id` references a Payment in the Payment service.
- **`idempotency_keys`** is the audit mirror of the Redis store. The active TTL lives in Redis; this table receives the entry on completion for compliance/forensics. Cleanup runs after 90 days.
- **`outbox_messages`** is the heart of [ADR-0004](../adr/0004-outbox-pattern.md). The publisher behaviour and the row lifecycle are in [docs/database/outbox.md](outbox.md).

## Indexes

| Table | Index | Reason |
|---|---|---|
| `transactions` | unique on `(tenant_id, order_reference)` | merchant-level uniqueness |
| `transactions` | btree on `(tenant_id, created_at desc)` | dashboard listing |
| `transactions` | btree on `(tenant_id, state)` | filtered listing |
| `transaction_state_history` | btree on `(transaction_id, at)` | timeline view |
| `refunds` | btree on `(transaction_id)` | per-transaction listing |
| `idempotency_keys` | unique on `(tenant_id, request_path, idempotency_key)` | the dedup index |
| `outbox_messages` | partial btree on `(state, next_attempt_at)` where `state in ('Pending','Failed')` | the publisher's hot query |
| `outbox_messages` | btree on `published_at` | retention cleanup |

## PII flags

Fields annotated `[PII]` in the schema above are treated as opaque personal data: not indexed, not searched, only stored and echoed back to the merchant in their own response/webhook envelope.

The current PII surface in this context is small:

| Field | Why it's PII |
|---|---|
| `transactions.metadata` | Merchant pass-through. May contain customer identifiers in practice; we do not parse it. |

Retention follows the host transaction: PII fields are purged or tombstoned when the transaction row is purged. A dedicated retention policy doc is intentionally out of scope for the reference implementation — a production deployment would add one (defining retention windows, KVKK/GDPR-aligned tombstones, and the right-to-erasure path).
