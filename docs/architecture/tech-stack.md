# Technology Selection Matrix

The choices below are the things you would have to defend in an architecture review. Each row gives the role the technology plays, the main alternatives we considered, and the trade-off we accepted. Where a choice was load-bearing enough to warrant a long-form decision, the ADR link is given.

The point of this page is not to advertise the stack; it's to make explicit that every choice has a cost and we know what we paid.

---

## Runtime & language

| Tech | Role | Considered | Why this | Trade-off accepted |
|---|---|---|---|---|
| **.NET 8 / C#** | Primary backend runtime | .NET 7, Java 21 + Spring Boot, Go | LTS, best-in-class performance for the workload, strong type system, the team's existing fluency | Slightly heavier container images than Go; cold start matters less on always-on workloads |
| **ASP.NET Core (Minimal APIs + MVC)** | HTTP layer | Carter, FastEndpoints | First-party, no extra abstraction layer to learn, OpenAPI tooling mature | Minimal APIs encourage anaemic handler files; we counter with MediatR |
| **YARP** | Reverse proxy / API gateway | Ocelot, Envoy, NGINX | First-party .NET, configurable from code, embeddable health checks | Less of an ecosystem than NGINX/Envoy for non-.NET shops |

---

## Data

| Tech | Role | Considered | Why this | Trade-off accepted |
|---|---|---|---|---|
| **PostgreSQL 16** | Primary OLTP store, one schema per service | MySQL, SQL Server | Mature, free, predictable, `jsonb` for opaque provider payloads, `pgvector` adds vector search in the same database | Operational tuning is on us; no managed serverless option |
| **pgvector** | Embedding storage for the AI assistant | Pinecone, Qdrant, Weaviate | Single database to operate, transactional consistency between text and vectors, sufficient at the scales this project demonstrates | Index types more limited than a dedicated vector DB; HNSW is enough for our use |
| **EF Core 8** | ORM | Dapper, raw ADO.NET | Productive for command-side, good migration story, integrates with multi-tenancy filters | Query plan surprises on complex LINQ; we drop to SQL where it matters (Reporting projections) |
| **Redis 7** | Idempotency keys, rate-limit counters, ephemeral cache | Memcached, in-process cache | Atomic ops needed for idempotency (`SETNX` + TTL), persistence not required | Adds an infra component; we keep usage narrow |

[ADR-0002](../adr/0002-database-per-service.md) captures the database-per-service decision and why we share a single PG instance in dev while separating in prod.

---

## Messaging

| Tech | Role | Considered | Why this | Trade-off accepted |
|---|---|---|---|---|
| **Apache Kafka** | Domain integration events, durable + replayable | RabbitMQ exchanges, NATS JetStream, AWS SNS+SQS | Replay for read-side projections, consumer groups, partitioning by `TenantId` for scale, schema discipline encouraged | Higher operational floor than RabbitMQ; we accept it for the eventing backbone |
| **RabbitMQ** | Notification retry queue with priorities | Kafka, Hangfire-only | Native priorities, per-message TTL, simple DLX setup | Two brokers to run; rationale in ADR-0003 |
| **Confluent.Kafka** | Kafka .NET client | MassTransit-over-Kafka | Lower-level but transparent: we can see exactly what is sent | More boilerplate; absorbed into `PayFlow.EventBus.Kafka` |
| **MassTransit** | RabbitMQ abstraction | Direct RabbitMQ.Client | Saga support, retry policies, correlation handling out of the box | Magic when something goes wrong; offset by the simpler messaging shape on this side |

---

## Cross-cutting

| Tech | Role | Considered | Why this | Trade-off accepted |
|---|---|---|---|---|
| **MediatR** | In-process CQRS dispatch | Direct service calls, Brighter | Clean separation of command/query handlers, pipeline behaviours for cross-cutting concerns | Indirection is a real cost; we use it only inside Application layers, never as a general event bus |
| **FluentValidation** | Request validation | DataAnnotations | Composable, testable, supports async rules | Slight learning curve compared to attributes |
| **Polly** | Retry, circuit breaker, timeout | Hand-rolled | Mature, well-known patterns, integrates with `HttpClientFactory` | One more concept in the stack; pays for itself in payment adapters |
| **Hangfire** | Recurring jobs (reconciliation) and refund retries | Quartz.NET, raw `BackgroundService` | Persistent jobs across restarts, dashboard for ops, easier than Quartz | SQL-backed storage adds load to the database (small in our case) |
| **Serilog** | Structured logging | Microsoft.Extensions.Logging only, NLog | Enrichers for tenant/trace context, multiple sinks, JSON output mature | Two logging abstractions in the stack; we keep MEL as the entry point and wire Serilog as provider |
| **OpenTelemetry** | Tracing + metrics | Vendor SDKs (DataDog, New Relic) | Vendor-neutral, OTLP everywhere, traces follow Kafka headers | Less polished than a paid APM; we accept it for portability |
| **Jaeger** | Trace storage and UI | Zipkin, Tempo, paid SaaS | Open source, simple to run locally, OTLP ingest | Storage is volatile; in prod we'd back with ES or replace |
| **Seq** | Local structured log viewer | ELK, Grafana Loki | One-binary, query language matches Serilog output | Single-tenant; we use it for dev, prod logs go elsewhere |
| **Grafana** | Metrics dashboards | Vendor SaaS | Free, runs locally, OTLP/Prometheus compatible | Configuration sprawl over time |

---

## AI

| Tech | Role | Considered | Why this | Trade-off accepted |
|---|---|---|---|---|
| **OpenAI API + Anthropic API** | LLM providers | Single-provider lock-in | Two providers behind an abstraction protects against rate-limit or pricing surprise | Two SDKs to keep up to date |
| **Embedding model: `text-embedding-3-small`** | RAG embeddings | Locally hosted models | Hosted, 1536-dim, cost-effective | External dependency for ingestion; mitigated by batch ingestion |

[ADR-0005](../adr/0005-llm-provider-abstraction.md) explains the LLM provider abstraction.

---

## Testing

| Tech | Role | Considered | Why this | Trade-off accepted |
|---|---|---|---|---|
| **xUnit** | Test runner | NUnit, MSTest | Defacto standard, parallelisation works | Minor; ecosystem is the same |
| **Moq** | Mocking | NSubstitute, FakeItEasy | Familiarity | Some advise NSubstitute; we don't see a difference on this scale |
| **Testcontainers** | Real Postgres + Kafka in integration tests | Sqlite, in-memory | Avoids the class of bugs where mocks pass and prod fails on a real migration | Slow tests; we keep the integration suite small and fast-feedback loops on unit tests |
| **FluentAssertions** | Assertion style | xUnit's `Assert` | Readable failure messages | Slightly opinionated |

---

## Infrastructure

| Tech | Role | Considered | Why this | Trade-off accepted |
|---|---|---|---|---|
| **Docker** | Container runtime | Podman | Standard | Standard |
| **docker-compose** | Local stack | Tilt, Skaffold | Lowest-friction onboarding for someone running this from a clean clone | Doesn't scale to prod; we use K8s there |
| **Kubernetes** | Production runtime | ECS, Nomad | Industry standard, demonstrates the patterns most relevant for a portfolio | Operational complexity; we accept it because the patterns matter |
| **GitHub Actions** | CI/CD | Azure Pipelines, GitLab CI | Free for public repos, sufficient feature set | YAML quirks |

---

## Excluded on purpose

A few choices that look obvious but we said no to:

- **gRPC for internal calls.** REST + JSON for inter-service is slightly more bytes on the wire, but every payload is human-debuggable. The volume between services is not the bottleneck.
- **GraphQL for the merchant API.** Our shape is operational, not catalogue-like. REST resources match the use cases. GraphQL would mostly add federation complexity.
- **Event Sourcing as the storage model.** We use the outbox pattern for reliable publishing and CQRS projections for reads, but the write side is a normal aggregate-with-state model. Full event sourcing would be a separate decision the team is not ready to defend.
- **Service mesh (Istio / Linkerd).** We get mTLS and tracing from OpenTelemetry + ingress configuration; the mesh would solve problems we don't have at this scale.

If any of these change, they need their own ADR.
