# ADR-0003: Kafka *and* RabbitMQ side by side

- **Status:** Accepted
- **Date:** 2025-12-15
- **Deciders:** Tech lead (solo)
- **Related:** [ADR-0004](0004-outbox-pattern.md) (Outbox)

## Context

We have two distinct messaging needs:

1. **Cross-service integration events.** "TransactionCaptured", "RefundCompleted", "PaymentSettled". These are durable, multiple-consumer, replay-able. Reporting joins late, projects from scratch. Reconciliation reads them. Notification reads them. None of these are tightly coupled in time.

2. **Notification retry queue.** Failed email/SMS deliveries need to be retried with backoff, optionally with priority (a refund email beats a marketing reminder), and eventually moved to a DLQ. Single producer, single consumer, short-lived.

A single broker that does both well does not exist. Kafka handles #1 beautifully and #2 awkwardly (no native delayed re-delivery, no priorities, retention semantics force you to hack TTL). RabbitMQ handles #2 beautifully and #1 awkwardly (no replay for new consumers, no log semantics, partitioning is not a first-class concept).

## Decision

Run both. **Kafka** for integration events between services. **RabbitMQ** for the notification retry queue.

- Integration event producers publish via the outbox pattern ([ADR-0004](0004-outbox-pattern.md)) to Kafka topics named `payflow.<context>.<event-name>.v1`. Partitioned by `TenantId`.
- Notification consumes from Kafka, attempts delivery, and on failure pushes to a RabbitMQ queue with a delayed-redelivery plugin. Retry budget is bounded; exhausted attempts go to a DLQ that humans inspect.

## Consequences

### Positive

- Each broker is used for the job it is good at. We avoid the contortions of forcing one model onto the other.
- The portfolio demonstrates both technologies, which matters for the audience this project is written for.
- Notification's retry/DLQ behaviour is testable and operationally observable using RabbitMQ's existing tooling (queue depths, dead-letter exchange, management UI).
- Integration events stay clean: a new read-side consumer can subscribe and replay from the beginning without disturbing existing consumers.

### Negative

- **Two brokers to operate.** Each adds memory, configuration, monitoring. For a real team this would be the dominant cost; we accept it because the project's purpose is to show the patterns.
- **Two messaging abstractions in the codebase** (`PayFlow.EventBus.Kafka` and `PayFlow.EventBus.RabbitMQ`). Mitigated by keeping interfaces narrow and intent-revealing (`IIntegrationEventBus` vs `INotificationQueue`).
- **Knowledge load on new contributors.** Two sets of failure modes to learn (partition rebalancing vs queue blocking, log retention vs message TTL).

### Neutral

- We could reconsider once the notification side either stops needing priorities or grows enough that a dedicated job framework (like Hangfire, which we already use for scheduled jobs) would absorb the use case. Today it doesn't fit Hangfire cleanly because the work is event-driven, not schedule-driven.

## Alternatives considered

### Kafka only

Use a delayed-retry topic with intermediate "retry" topics per backoff bucket (5s, 30s, 5m). Works, well-known pattern. Rejected because:

- Priorities are not expressible without explicitly modelling them as separate topics, which means N producers per priority. Ugly.
- The retry behaviour becomes a thing to reason about in *every* consumer that wants retries, not just notification.
- DLQ semantics still need to be hand-rolled.

### RabbitMQ only

Use streams for event-source-like consumption. Considered. Rejected because:

- Streams are newer and less battle-tested for the partition-by-key + replay scenarios we want.
- The tooling and operational mindshare for Kafka in the event-driven space is much larger; demonstrating it is more valuable for the portfolio audience.

### A managed service (AWS EventBridge / SNS+SQS)

Out of scope. We are not pinning the project to a cloud vendor, and the self-hosted version is the version this codebase demonstrates.

## Notes

The honest summary: a small startup would pick one broker and live with one set of compromises. We pick two because (a) the workloads are genuinely different and (b) the project is *about* showing the patterns, and the patterns are different.

If this were a real product reviewed in a year, the question "could we collapse to Kafka-only now" would be a fair one. The retry topic pattern has gotten better; priorities can sometimes be redesigned away. We will not pre-litigate it.
