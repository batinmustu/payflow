# ADR-0001: Microservices over Modular Monolith

- **Status:** Accepted
- **Date:** 2025-12-04
- **Deciders:** Tech lead (solo)

## Context

PayFlow is a portfolio reference implementation, but the architectural choices have to defend themselves as if a real team would build on them. There are two plausible starting points:

1. **Modular monolith.** One deployable, well-bounded modules inside it, in-process calls between modules, single database, future extraction if needed.
2. **Microservices.** Separate deployables per bounded context from day one, integration over Kafka, database-per-service.

Most thoughtful guidance for *real* greenfield projects today is "start with a modular monolith and extract later". That is not the trade-off being made here.

Constraints specific to this project:

- The product is multi-tenant and has clear bounded contexts that map to obvious deployment boundaries (Identity, Payment, Transaction, Reconciliation, Reporting, Notification, AI Assistant).
- Two of those contexts have qualitatively different scaling profiles. Reporting is read-heavy and elastic. Payment is bursty around campaigns. They do not benefit from being co-deployed.
- The AI Assistant has a fundamentally different release cadence (prompt updates, model swaps) than the rest of the system.
- The portfolio audience wants to see microservice patterns implemented in earnest: outbox, integration events, idempotency, distributed tracing. A modular monolith hides exactly the parts a reviewer is looking at.

## Decision

Build PayFlow as **microservices** from day one. Seven services plus an API gateway, communicating through:

- synchronous HTTPS for command-style internal calls where the caller needs the result (Transaction → Payment is the main example);
- asynchronous integration events over Kafka for everything else.

Each service has its own schema (initially in a shared Postgres instance in dev; separate instances in prod — see ADR-0002).

## Consequences

### Positive

- Each context can evolve, scale and fail independently.
- The patterns most relevant to senior-level review (outbox, idempotency, distributed tracing, eventual consistency) are forced to the surface rather than swept under in-process method calls.
- The team boundary "this service is yours" maps cleanly to ownership; even at one engineer, this future-proofs handoffs.
- Multi-tenancy isolation is enforced both at the schema level and at the service boundary; harder to leak by accident.

### Negative

- **Operational cost is real.** Seven services + brokers + observability stack is a lot to run in dev. We mitigate with `docker-compose` and a "minimal stack" subset.
- **Debugging spans process boundaries.** Distributed tracing is non-optional; this is a cost paid in OpenTelemetry setup and trace-context propagation through Kafka headers.
- **Local development is slower than a monolith.** A first-time clone has to pull more images. The local-stack doc minimises the surprise.
- **Cross-cutting changes touch many repos.** Mitigated by keeping shared kernels intentionally small (see [bounded-contexts.md](../architecture/bounded-contexts.md)).
- **Eventually consistent reads.** Reporting is always behind Transaction by some milliseconds. The product is designed to tolerate this; UI affordances make it clear.

## Alternatives considered

### Modular monolith

The honest answer to "what would I recommend for a real startup with a small team?" — but it wins on different criteria. We deliberately do not use those criteria here. If this were a paying-customer startup with 2 engineers, the choice would flip.

### Service-oriented "macroservices" (3-4 larger services)

Considered as a middle ground. Rejected because the obvious cleavages (Identity vs Payment vs Transaction vs Reconciliation) already produce reasonably small services. Merging Reconciliation back into Transaction, for example, would re-import the very thing we want to separate (scheduled jobs polluting the request path).

### Function-per-endpoint serverless

Not considered seriously. We are demonstrating a stateful, brokered, observably traced .NET system. Serverless would replace half the interesting parts with cloud-vendor magic and inverts the deployment story (`docker-compose` is no longer the local equivalent of prod).

## Notes

This ADR's main job is to mark the moment we accepted the operational cost rather than letting it accrete by accident. Every subsequent decision (Kafka, outbox, multi-tenancy strategy, idempotency) assumes this baseline. If we ever decide to merge two services, that should be a new ADR that explicitly cites this one.
