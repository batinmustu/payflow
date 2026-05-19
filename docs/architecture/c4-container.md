# C4 — Container Diagram (Level 2)

Zooming one level into the PayFlow box from [c4-context.md](c4-context.md). Each "container" here is a separately deployable unit: an ASP.NET Core service, a worker process, a database, or a broker. Within each container, the internal structure (Domain / Application / Infrastructure / API) is the same Clean Architecture layout — that's covered in [docs/onboarding/coding-standards.md](../onboarding/coding-standards.md).

```mermaid
flowchart TB
    classDef svc fill:#1168bd,stroke:#0b4884,color:#fff
    classDef worker fill:#3b7ad6,stroke:#0b4884,color:#fff
    classDef db fill:#7c4dff,stroke:#4b27a8,color:#fff
    classDef broker fill:#f7a35c,stroke:#a06f3a,color:#000
    classDef external fill:#999,stroke:#666,color:#fff
    classDef obs fill:#cfcfcf,stroke:#888,color:#000

    subgraph Edge["Edge"]
        GW["API Gateway<br/>(YARP)"]:::svc
    end

    subgraph Services["Application services"]
        IDENT["Identity Service<br/>(.NET 8)"]:::svc
        PAY["Payment Service<br/>(.NET 8)"]:::svc
        TX["Transaction Service<br/>(.NET 8)"]:::svc
        REC["Reconciliation Service<br/>(.NET 8 + Hangfire)"]:::svc
        REPORT["Reporting Service<br/>(.NET 8)"]:::svc
        AI["AI Assistant Service<br/>(.NET 8)"]:::svc
    end

    subgraph Workers["Background workers"]
        OUTBOX_TX["Outbox Publisher<br/>(in Transaction)"]:::worker
        OUTBOX_PAY["Outbox Publisher<br/>(in Payment)"]:::worker
        NOTIF_W["Notification Worker<br/>(.NET 8 BackgroundService)"]:::worker
        REC_JOB["Reconciliation Job<br/>(Hangfire)"]:::worker
    end

    subgraph Data["Data stores"]
        PG[("PostgreSQL 16<br/>schema-per-service<br/>+ pgvector")]:::db
        REDIS[("Redis 7<br/>idempotency, rate limits")]:::db
    end

    subgraph Brokers["Messaging"]
        KAFKA[["Kafka<br/>Integration events"]]:::broker
        RMQ[["RabbitMQ<br/>Notification retries"]]:::broker
    end

    subgraph Obs["Observability"]
        JAEGER[("Jaeger<br/>traces")]:::obs
        SEQ[("Seq<br/>logs")]:::obs
        GRAFANA[("Grafana<br/>metrics")]:::obs
    end

    subgraph External["External"]
        PROV["Iyzico / Stripe / PayPal<br/>(mocks)"]:::external
        LLM["OpenAI / Anthropic"]:::external
        SMTP["SMTP / SMS"]:::external
    end

    GW --> IDENT
    GW --> PAY
    GW --> TX
    GW --> REPORT
    GW --> AI
    GW --> REDIS

    TX --> PAY
    TX --> PG
    TX --> REDIS

    PAY --> PG
    PAY --> PROV

    IDENT --> PG

    REC --> PG
    REC --> PROV
    REC_JOB --> REC

    REPORT --> PG

    AI --> PG
    AI --> LLM

    OUTBOX_TX --> PG
    OUTBOX_TX -- "publish" --> KAFKA
    OUTBOX_PAY --> PG
    OUTBOX_PAY -- "publish" --> KAFKA

    KAFKA --> REPORT
    KAFKA --> REC
    KAFKA --> NOTIF_W

    NOTIF_W --> RMQ
    NOTIF_W --> SMTP

    Services -. "OTLP traces" .-> JAEGER
    Services -. "Serilog JSON" .-> SEQ
    Services -. "metrics" .-> GRAFANA
```

## Containers

| Container | Tech | Responsibility |
|---|---|---|
| **API Gateway** | YARP on .NET 8 | Single ingress. Validates JWTs against Identity's public keys (no callback per request), enforces rate limits via Redis, forwards to the matching service. |
| **Identity Service** | ASP.NET Core | Issues JWTs, manages tenants/users/roles and API keys. Owns its schema in Postgres. |
| **Payment Service** | ASP.NET Core | Wraps the three provider adapters behind a common `IPaymentProvider` interface. Stores provider credentials encrypted at rest. Synchronous to its callers; publishes `payment.completed.v1` to downstream observers through its own outbox. |
| **Transaction Service** | ASP.NET Core | The aggregate of record for a payment attempt. Holds the state machine, the outbox, idempotency keys, the routing engine. Calls Payment synchronously. |
| **Reconciliation Service** | ASP.NET Core + Hangfire | Hangfire scheduler hosting daily statement-fetch jobs. Also the refund saga coordinator. |
| **Reporting Service** | ASP.NET Core | CQRS read side. Consumes integration events, maintains projections in its own schema, serves dashboard queries. |
| **AI Assistant Service** | ASP.NET Core | RAG pipeline. Owns the embeddings schema (pgvector). Streams responses over SSE. |
| **Notification Worker** | .NET 8 BackgroundService | Consumes Kafka integration events, renders templates, sends through SMTP/SMS, retries via RabbitMQ. |
| **Outbox Publisher** | In-process worker, one per producing service (currently Transaction and Payment) | Polls the local outbox table, publishes to Kafka, marks rows published. See [docs/database/outbox.md](../database/outbox.md). |

## Data stores

**PostgreSQL** is one physical instance in dev, separated per-service in prod. Each service owns one schema and uses its own DB user; no cross-schema queries. The AI Assistant schema also hosts the pgvector extension.

**Redis** is single-instance, used narrowly: idempotency-key storage (with TTL) and gateway rate-limit counters. Nothing in Redis is the source of truth for anything.

## Messaging

**Kafka** carries every cross-service integration event. Topics are partitioned by `TenantId` so a tenant's events stay ordered. The catalogue is in [docs/events/catalog.md](../events/catalog.md).

**RabbitMQ** carries notification retries with priorities and TTL. Notification is the only producer and consumer. The reason both brokers exist is documented in ADR-0003.

## Observability sinks

Application services emit OTLP traces to Jaeger, structured logs to Seq, and metrics scraped by a Prometheus-compatible Grafana setup. Configuration lives in `PayFlow.Observability` and is wired identically per service.

## What this diagram does not yet show

- Replicas / horizontal scaling — every service is stateless and runs N replicas; that is a deployment-time concern.
- The internal Clean Architecture split of each service — that's a Level 3 diagram and we only produce it for two services where the structure is non-trivial (Transaction and Payment); see `docs/diagrams/c4-component-*.puml` (planned).
- Helm/K8s manifests — see [docs/devops/](../devops/).
