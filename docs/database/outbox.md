# Outbox — Schema, Lifecycle, and Publisher Behaviour

The transactional outbox is the mechanism that makes "publish on commit" actually safe. The decision rationale is in [ADR-0004](../adr/0004-outbox-pattern.md); this doc is the operational and schema reference.

## Schema (recap)

The full DDL lives in the Transaction service migration; the relevant columns:

| Column | Type | Meaning |
|---|---|---|
| `id` | uuid | message id, used by consumers for deduplication |
| `tenant_id` | uuid | for partitioning and tenant-scoped retention |
| `aggregate_type` | text | `Transaction` or `Refund` today |
| `aggregate_id` | uuid | the entity that produced the event |
| `event_type` | text | `payflow.transaction.captured.v1`, etc. |
| `payload` | jsonb | the serialised integration event body |
| `headers` | jsonb | trace id, causation id, tenant id, version |
| `state` | text | `Pending`, `Publishing`, `Published`, `Failed` |
| `attempt_count` | int | for backoff |
| `last_error` | text | exception summary on the latest attempt |
| `created_at` | timestamptz | written in the same transaction as the business state change |
| `next_attempt_at` | timestamptz | publisher does not pick a row before this |
| `published_at` | timestamptz | success timestamp |

The hot query is "give me pending rows due now":

```sql
SELECT id, payload, headers, event_type
FROM outbox_messages
WHERE state IN ('Pending', 'Failed')
  AND next_attempt_at <= now()
ORDER BY created_at
LIMIT 100
FOR UPDATE SKIP LOCKED;
```

`SKIP LOCKED` is the critical bit — multiple publisher instances can run concurrently and each will pick up disjoint rows.

The lifecycle in words:

```
Pending → Publishing → Published      (happy path)
Pending → Publishing → Failed → ...   (transient error, retried)
Failed (terminal) → human intervention (after max attempts)
```

## The publisher worker

Each service that produces outbox messages hosts an `OutboxPublisher` as a `BackgroundService`. Today that is **Transaction** (lifecycle and refund events) and **Payment** (per-attempt `payment.completed.v1`). Each runs its own publisher against its own schema; they do not share a worker. The behaviour:

1. **Poll interval.** Default 500ms. Configurable per service. A poll that finds work loops immediately without waiting; a poll that finds nothing waits the interval.
2. **Claim batch.** Up to 100 rows per tick, using `FOR UPDATE SKIP LOCKED`. Each claimed row is updated to `Publishing` in the same transaction as the claim, so a crash mid-publish leaves the row in `Publishing` — see "Recovery" below.
3. **Publish.** Each row's payload is published to Kafka via `IProducer<string, byte[]>`. The Kafka partition key is the `TenantId` so a tenant's events stay ordered.
4. **Acknowledge.** On Kafka producer's delivery report (`ack`), the worker updates the row to `Published` and sets `published_at`.
5. **Failure.** On exception (broker unavailable, serialisation error), the row goes to `Failed` with `attempt_count` incremented and `next_attempt_at` pushed forward per the backoff schedule.

### Backoff schedule

| Attempt | Next attempt delay |
|---|---|
| 1 → 2 | 5 seconds |
| 2 → 3 | 30 seconds |
| 3 → 4 | 2 minutes |
| 4 → 5 | 10 minutes |
| 5 → 6 | 1 hour |
| 6 → 7 | 6 hours |
| 7+ | terminal (alert) |

The terminal alert fires through the same observability stack as runtime errors. A terminal outbox message is treated as a P2 incident — it implies a downstream consumer has been missing this event for hours.

## Recovery from publisher crash

The window of danger is between "claimed the row, set state to Publishing" and "got the broker ack, set state to Published". If the worker dies in that window, the row stays in `Publishing` indefinitely without intervention.

The startup sweep handles this:

- On worker start, before the main loop, the worker scans for rows in `Publishing` older than a threshold (default 60 seconds) and resets them to `Pending` with `attempt_count` decremented (so the retry budget is not consumed by a crash that was not the broker's fault).
- The threshold is deliberately wide; the trade-off is "duplicate publish" vs "lost publish". The outbox pattern is at-least-once, so duplicates are the safer side to err on, and consumers dedupe by `MessageId`.

## Retention

Published rows are not deleted immediately. A daily cleanup job removes rows with `state = 'Published'` older than 7 days. The retention window exists so we can replay recent events (e.g. when adding a new consumer) without restoring from backup.

`Failed` rows in terminal state are kept until a human triages them.

## Operational metrics

The publisher emits the following metrics (Prometheus naming):

| Metric | Type | Labels | Use |
|---|---|---|---|
| `outbox_pending_count` | gauge | `service`, `tenant_id` (sampled, not per-tenant) | depth of backlog |
| `outbox_publish_latency_seconds` | histogram | `service` | end-to-end produce time |
| `outbox_publish_failures_total` | counter | `service`, `error_class` | retry tracking |
| `outbox_terminal_count` | gauge | `service` | the alarm metric |

The alarm condition: `outbox_terminal_count > 0 for 5m`. The response is "look at `last_error`, fix the root cause, manually transition rows back to `Pending` if the error is now resolvable; otherwise treat the message as undeliverable and reconcile downstream consumers by hand".

## Why this is a library, not a service

Each producing service hosts its own publisher because the polling needs to happen against its own database. A shared "outbox-as-a-service" would need credentials to read every producer's outbox table, which is exactly the cross-schema coupling the [database-per-service rule](../adr/0002-database-per-service.md) exists to prevent.

The shared code lives in `PayFlow.Outbox` — the schema migration is a static helper a service applies, and the publisher is a `BackgroundService` the service hosts. Configuration (poll interval, max attempts, backoff) is per service.
