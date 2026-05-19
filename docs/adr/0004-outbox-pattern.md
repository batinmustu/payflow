# ADR-0004: Transactional Outbox for reliable integration event publishing

- **Status:** Accepted
- **Date:** 2026-01-12
- **Deciders:** Tech lead (solo)
- **Related:** [ADR-0001](0001-microservices-vs-modular-monolith.md), [ADR-0002](0002-database-per-service.md), [ADR-0003](0003-kafka-and-rabbitmq.md)

## Context

The Transaction service is the source of truth for `TransactionInitiated`, `TransactionAuthorized`, `TransactionCaptured`, `TransactionFailed`, and the refund events. Downstream services (Reporting, Notification, Reconciliation) depend on these being delivered.

The naive implementation is "write to the database, then publish to Kafka". Three things can go wrong:

1. DB write succeeds, the service crashes before publishing. The event is lost.
2. DB write succeeds, the publish fails transiently. Without bookkeeping, the event is lost.
3. Publish succeeds, the DB transaction rolls back afterward. A downstream observer reacts to a state change that never happened.

Any one of these is enough to corrupt the downstream projections. We need delivery semantics that are at-least-once with respect to events, and we need them to be aligned with the local DB transaction.

## Decision

Adopt the **transactional outbox pattern**.

- Every integration event-producing operation writes the business state change and an `outbox_messages` row in the *same* DB transaction.
- A background worker (`OutboxPublisher`) polls unpublished rows, publishes to Kafka, and marks them published on broker acknowledgement.
- Consumers are responsible for idempotency (matching `MessageId` to suppress duplicates).

This is implemented once in `PayFlow.Outbox` and reused by any service that publishes integration events. Today both **Transaction** and **Payment** use it — Payment publishes `payflow.payment.completed.v1` per adapter call, and the same lost-event problem applies, so the same answer applies. Any new event-producing service adopts the outbox as a default and has to provide a counter-argument if it intends not to.

The full schema, polling cadence, retry behaviour, and lifecycle are in [docs/database/outbox.md](../database/outbox.md).

## Consequences

### Positive

- Event publication is now part of the same DB transaction as the state change. Either both happen or neither does.
- A service crash between commit and Kafka publish does not lose events; the next worker tick picks them up.
- Replays are possible: an event can be republished by resetting the published flag, useful for backfilling a new consumer.
- The producer side of every event has the same shape, so the library handles it.

### Negative

- **Latency floor.** An event is not on Kafka until the polling worker picks it up (poll interval is configurable; default 500ms). For our use cases this is acceptable; for sub-100ms downstream reactions it would not be. We are not designing for that.
- **Database load.** The outbox table sees one insert per event and one update per publish. With reasonable retention and a partial index on unpublished rows, this is small. We monitor it.
- **Consumer idempotency is non-optional.** At-least-once is the floor; "exactly once" is a fairy tale and we do not pretend otherwise. Each consumer dedupes by `MessageId`, which is carried as a header on every published event.

## Alternatives considered

### Listen/notify (Postgres `LISTEN`/`NOTIFY`) instead of polling

Works for a single subscriber, breaks if multiple workers want to share the load, and ties the publisher tightly to Postgres semantics. The polling approach is uglier but generalises across stores and survives broker outages without losing the work.

### Debezium / change data capture

Reads the Postgres WAL and emits events to Kafka automatically. Compelling and gaining adoption. Rejected for now because:

- It is operationally heavier than the outbox table + polling worker.
- It infers events from row changes rather than receiving them as first-class messages. We want events to be explicit, deliberately shaped, and versioned, not a side effect of column updates.
- The portfolio audience is more familiar with the outbox pattern; demonstrating it has more pedagogical value.

### Two-phase commit between Postgres and Kafka

Possible in theory with XA, awful in practice. Not seriously considered.

### "Just publish and reconcile later"

The reconciliation service exists, but its job is provider settlement reconciliation, not "did Kafka receive our events". Building a comparison job between the DB and Kafka to detect missed events is exactly the outbox pattern with extra steps.

## Notes

The reason this is its own ADR rather than a paragraph in ADR-0003: every service that ever wants to publish integration events will face this decision. Capturing the answer once means we do not relitigate it per service.

The Transaction outbox is the more interesting case (multi-event lifecycle, refund saga interactions) and the rest of the docs lean on it as the worked example. The Payment outbox is the same pattern with a smaller event set; it lives in Payment's own schema with its own polling worker, isolated from Transaction's by [ADR-0002](0002-database-per-service.md).
