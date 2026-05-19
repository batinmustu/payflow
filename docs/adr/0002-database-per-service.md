# ADR-0002: Database per service, one Postgres instance in dev

- **Status:** Accepted
- **Date:** 2025-12-08
- **Deciders:** Tech lead (solo)
- **Related:** [ADR-0001](0001-microservices-vs-modular-monolith.md), [ADR-0004](0004-outbox-pattern.md)

## Context

[ADR-0001](0001-microservices-vs-modular-monolith.md) commits to microservices. That decision has a follow-up question that does not answer itself: what is the data ownership boundary?

Three candidates:

1. **One shared database, services share tables.** The thing every "we have microservices" team eventually regrets. Every service can read every other service's tables; refactoring one schema requires coordinating releases across services. The bounded contexts in the code stop being bounded contexts in the data.
2. **One database, one schema per service, no cross-schema reads.** Physical co-location with logical separation. Each service has its own EF `DbContext`, its own migration history, its own DB role with `GRANT` only on its own schema.
3. **One database instance per service.** Maximal isolation. Independent scaling, independent backups, no risk of a noisy neighbour. Significant operational overhead in dev.

PayFlow needs cross-service event flow (Kafka, see [ADR-0003](0003-kafka-and-rabbitmq.md)) and reliable publish-on-commit ([ADR-0004](0004-outbox-pattern.md)). Both are easier when each producer owns its own write store outright. Both still work if multiple services happen to share a Postgres instance, as long as they do not read each other's schemas.

## Decision

**Database per service**, with the physical packaging adjusted by environment:

- **Dev / local:** one Postgres 16 instance, one database (`payflow`), seven schemas — one per service. Each service uses its own DB role; the role has `GRANT` only on its own schema. Cross-schema queries are forbidden by permission, not by convention.
- **Production:** one Postgres instance per service (or at minimum, well-isolated databases on shared infrastructure with separate credentials and connection pools). The schema-per-service layout maps one-to-one onto separate instances; no application code changes.

The AI Assistant schema additionally hosts the `pgvector` extension; the dev instance is built with the extension installed.

Cross-service data flow is **always** via integration events on Kafka. There is no "service A queries service B's DB" path, in dev or in prod.

## Consequences

### Positive

- **Schemas are owned.** A breaking change in Transaction's tables affects Transaction and nothing else. Coordination with consumers happens through the integration event contract in `PayFlow.Contracts`, which is versioned.
- **Outbox is local.** The outbox table sits in the same schema as the business state it tracks, so "write business state and outbox row in one transaction" is a single-database transaction, not a distributed one.
- **Dev is cheap.** One Postgres container, one volume, one set of credentials. New contributors don't have to provision seven databases on their laptop.
- **Prod is honest.** When we promote to per-instance, the application sees the same shape: a connection string per service. The K8s manifests / Helm charts differ in the number of database resources, not in how services are wired.

### Negative

- **The "one instance" dev mode hides noisy-neighbour problems.** A migration that locks a large table in dev only locks one service's schema; in prod it would only block that service's instance, but the cost of the migration itself can be different on a larger production dataset. We accept this; the integration tests in CI use Testcontainers per service, which exposes the per-instance shape.
- **Refreshing one service's data wipes the whole dev DB if you go via `down -v`.** The local-stack doc points out the alternative (drop and recreate a single schema). In practice we do `down -v` and start over.
- **Reporting reads have to stay disciplined.** Reporting projects from Kafka into its own schema. We do not let it shortcut by reading Transaction's tables directly, even though both schemas live in the same instance in dev. The lint check on `FromSql`/`ExecuteSqlRaw` and the per-service DB role together enforce this.
- **No cross-schema foreign keys.** A `transaction_payment_attempts.payment_id` points at a row in Payment's `payments` table, but it cannot be a real FK. We accept the referential-integrity gap and rely on reconciliation + the event flow to catch drift. The threat model captures this explicitly under "risks we accept".

## Alternatives considered

### Shared schema, table-level access control

Every service in one schema, with column- or table-level `GRANT`s. Considered for half a minute. The schema becomes a tragedy-of-the-commons design surface, and microservices stop meaning what they're supposed to mean. Rejected without a long argument.

### Database per service from day one, even in dev

The "purist" position. Seven Postgres containers in dev. Rejected because: the cost (memory, startup time, port management, seven backup commands) is real and the benefit (production-shape isolation) is largely captured by the per-schema layout plus a per-service DB role.

### Multi-database orchestration via something like Citus or a managed multi-tenant proxy

Out of scope. We do not need horizontal sharding at this size; the multi-tenancy strategy is at the row level (see [docs/database/multi-tenancy-isolation.md](../database/multi-tenancy-isolation.md)), not at the database level.

## Notes

The reason this is its own ADR rather than a paragraph in ADR-0001: the schema-vs-instance split is a deliberate dev/prod difference that surprises new contributors. Spelling it out once means we do not have to defend the dev shortcut on every PR that touches schema. The promotion path to per-instance production deployment is intentional, not accidental.
