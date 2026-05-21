# PayFlow

A reference implementation of a multi-tenant payment orchestration platform, built in .NET 8 microservices. It unifies three payment providers behind a single API using Strategy + Adapter patterns, processes transactions through an event-driven pipeline with reliable Kafka publishing via the outbox pattern, and includes an AI assistant that answers merchant questions using RAG.

The audience for this repo is *engineers reading the code*. The docs describe the architectural decisions in the way you would defend them in a review, not the way a marketing page would describe them.

## Status

| Milestone                                                  | State |
|------------------------------------------------------------|-------|
| M0 Foundation (sln, infra, observability)                  | ✅    |
| M1 Identity & Gateway                                      | ✅    |
| M2 Payment (provider Strategy + Adapter)                   | ✅    |
| M3 Transaction + Eventing (outbox + Kafka)                 | ✅    |
| M4 Reconciliation + refund saga                            | ✅    |
| M5 Reporting (CQRS read-side projections)                  | ✅    |
| M6 Notification (Kafka-driven email log)                   | ✅    |
| M8 Observability (Kafka trace propagation + log enrichers) | ✅    |
| M9 Deployment (Dockerfiles + prod compose + CI + K8s sample) | ✅  |
| Post-M9 hardening (see below)                              | ✅    |
| M7 AI Assistant (RAG over pgvector)                        | pending (sequenced last) |

Seven services online (Identity, Payment, Transaction, Reconciliation, Reporting, Notification, Webhooks) behind a YARP gateway. **190 unit + 16 integration tests green.** End-to-end traces span every service in one Jaeger view — Kafka is no longer a trace boundary.

**Post-M9 hardening** added the operational pieces a portfolio claim should be able to back up:

- Refund saga **recovery worker** + Testcontainers integration test (6 scenarios — happy path, deterministic decline, transient retry, recovery, idempotency, stuck-list gating).
- Notification **RabbitMQ retry queue + DLQ** — failed sends get exponential-backoff retries via a wait-queue/DLX/work-queue topology; falls back to a no-op queue when the broker isn't configured.
- **Helm umbrella chart** at `deploy/helm/payflow/` — one install renders Deployment + Service per service plus the shared ConfigMap + Secret.
- **OTel Collector + Prometheus + Grafana** wiring — services emit OTLP, the collector fans traces to Jaeger and exposes a Prometheus scrape endpoint; Grafana auto-provisions a service-overview dashboard.
- **k6 load scenarios** under `deploy/k6/` — smoke / transaction happy-path / refund-saga, gateway-fronted, JWT cached per VU.
- **Webhooks service (8th microservice)** — HMAC-SHA256 signed deliveries to merchant endpoints with a five-attempt retry budget; full subscription + delivery CRUD.

## The 30-second tour

```mermaid
flowchart LR
    M[Merchant API] --> G[Gateway]
    G --> T[Transaction]
    T --> P[Payment]
    P -.-> I[(Iyzico)]
    P -.-> S[(Stripe)]
    P -.-> PP[(PayPal)]

    T -->|outbox| K{{Kafka}}
    P -->|outbox| K
    RC -->|outbox| K
    K --> RP[Reporting]
    K --> NT[Notification]
    K --> RC[Reconciliation]
    K --> WH[Webhooks]
    NT -. retries .-> RMQ{{RabbitMQ}}
    WH -. POST + HMAC .-> MB[Merchant<br/>backend]

    A[AI Assistant] --> RP
    A -.-> LLM[(OpenAI / Claude)]
```

A merchant calls `POST /api/transactions`. Gateway authenticates, forwards to the Transaction service. Transaction picks a provider per the tenant's routing rule and calls Payment. Payment's adapter speaks the provider's protocol. On commit, Transaction writes an outbox row in the same DB transaction; a publisher worker delivers it to Kafka. Reporting, Notification, and Reconciliation react asynchronously.

The full runtime topology (ports, schemas, telemetry sinks) is in [docs/diagrams/runtime-topology.md](docs/diagrams/runtime-topology.md); the [end-to-end trace walkthrough](docs/diagrams/end-to-end-trace.md) follows one refund's spans through every service; the [payment happy path](docs/flows/payment-happy-path.md) is the request-level narrative.

## Getting started

```sh
# 1. Bring up infrastructure (Postgres, Kafka, Redis, RabbitMQ,
#    Jaeger + OTel Collector + Prometheus + Grafana, Seq)
docker compose -f deploy/docker-compose.infra.yml up -d

# 2. Build the solution
dotnet build PayFlow.sln

# 3. Run each service in its own shell (or background as you prefer)
dotnet run --project src/Services/Identity/PayFlow.Identity.API             --urls=http://127.0.0.1:5001
dotnet run --project src/Services/Payment/PayFlow.Payment.API               --urls=http://127.0.0.1:5002
dotnet run --project src/Services/Transaction/PayFlow.Transaction.API       --urls=http://127.0.0.1:5003
dotnet run --project src/Services/Reconciliation/PayFlow.Reconciliation.API --urls=http://127.0.0.1:5004
dotnet run --project src/Services/Reporting/PayFlow.Reporting.API           --urls=http://127.0.0.1:5005
dotnet run --project src/Services/Notification/PayFlow.Notification.API     --urls=http://127.0.0.1:5006
dotnet run --project src/Services/Webhooks/PayFlow.Webhooks.API             --urls=http://127.0.0.1:5007
dotnet run --project src/ApiGateway/PayFlow.Gateway                         --urls=http://127.0.0.1:5050
```

Each service applies its EF Core migrations on startup in Development. The Postman collection at [`docs/api/PayFlow.postman_collection.json`](docs/api/PayFlow.postman_collection.json) exercises every endpoint across the eight services; replay the requests in the order shown in the folders. Full local-stack notes (ports, healthcheck commands, observability sinks) live in [docs/devops/local-stack.md](docs/devops/local-stack.md).

Useful local URLs once everything is up: **Jaeger** at `http://localhost:16686`, **Grafana** at `http://localhost:3000` (admin / admin — service-overview dashboard auto-loaded), **Prometheus** at `http://localhost:9090`, **Seq** at `http://localhost:5341`, **RabbitMQ management** at `http://localhost:15672` (guest / guest).

## Containerised / production-shaped deploy

Everything above runs the services on the host with `dotnet run`. The `deploy/` folder has the production-shaped layout:

```sh
# 1. Fill in the secrets contract (JWT signing key, DB password, …)
cp deploy/.env.example deploy/.env
$EDITOR deploy/.env

# 2. Build all 8 service images + bring up the full prod-shaped stack
docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d --build
```

Only the gateway exposes a host port (`5050` by default); every other service is reachable only from inside the compose network by container name. The Dockerfile under `deploy/docker/Dockerfile.service` is parametric — one file builds every service via `--build-arg PROJECT_DIR=… PROJECT_NAME=…`. CI exercises this matrix on every push (`.github/workflows/ci.yml`).

For a Kubernetes target the umbrella Helm chart at [`deploy/helm/payflow/`](deploy/helm/payflow/README.md) renders Deployment + Service for each of the seven application services plus the shared ConfigMap + Secret in one install. Set `services.<name>.enabled=false` to drop a service, opt in to an Ingress for the gateway via `ingress.enabled=true`, see the chart README for the full values surface. A reviewable single-service starting point (Deployment + Service + ConfigMap + Secret with non-root + resource limits + liveness/readiness against `/health`) lives at `deploy/k8s/transaction.yaml`.

### Load testing

[`deploy/k6/`](deploy/k6/README.md) ships three k6 scenarios — `smoke.js`, `transaction-happy-path.js`, `refund-saga.js` — gateway-fronted, JWT cached per VU, configurable RPS via env vars. Run them while watching the Grafana dashboard to see the RPS / latency / heap pressure panels light up.

## Where to look

### Start here

| If you want to understand... | Go to |
|---|---|
| The shared vocabulary | [Glossary](docs/domain/glossary.md) |
| The service shape | [Bounded contexts](docs/architecture/bounded-contexts.md) |
| Why the architecture is what it is | [ADRs](docs/adr/) |
| What's in the stack | [Tech selection](docs/architecture/tech-stack.md) |

### Architecture

- [C4 — Context diagram](docs/architecture/c4-context.md) and [Container diagram](docs/architecture/c4-container.md)
- [Bounded contexts](docs/architecture/bounded-contexts.md)
- [Runtime topology](docs/diagrams/runtime-topology.md) — actual processes, ports, and wire-traffic arrows on the local stack
- [End-to-end trace walkthrough](docs/diagrams/end-to-end-trace.md) — one refund's spans across all services

### How the signature flows work

- [Payment happy path](docs/flows/payment-happy-path.md)
- [Refund saga](docs/flows/refund-saga.md)

### Data

- [Transaction ERD](docs/database/erd-transaction.md) — the central context's schema
- [Multi-tenancy isolation](docs/database/multi-tenancy-isolation.md)
- [Outbox schema](docs/database/outbox.md)

### Events

- [Event catalog](docs/events/catalog.md)
- [Transaction state machine](docs/state-machines/transaction.md)

### API contracts

- [Error codes](docs/api/errors.md)
- [Idempotency guide](docs/api/idempotency.md)
- [Webhook spec](docs/api/webhooks.md)

### Security

- [Threat model](docs/security/threat-model.md)

### AI

- [RAG architecture](docs/ai/rag-architecture.md)

### Deployment & operations

- [Helm umbrella chart](deploy/helm/payflow/README.md) — one install renders the whole fleet
- [k6 load scenarios](deploy/k6/README.md) — smoke / happy-path / refund saga
- [Observability stack](deploy/observability/) — OTel Collector + Prometheus + Grafana dashboard

### Onboarding

- [Coding standards](docs/onboarding/coding-standards.md)
- [Local stack](docs/devops/local-stack.md)

## What this project is — and isn't

It **is** a reference implementation of patterns commonly required in payment platforms: provider abstraction, intelligent routing, outbox-backed eventing, CQRS for read scale, multi-tenancy, RAG with tenant-scoped knowledge.

It **isn't** a PCI-DSS-certified payment processor, a chargeback handling system, a production-ready substitute for real Iyzico / Stripe / PayPal integrations, or a multi-region HA deployment.

## A note on docs

The first pass of docs is broader than the code — designs were written down before the implementation caught up. As the code lands, docs are tightened or removed; what stays here is what stays referenced from the code.

## License

[MIT](LICENSE) © 2026 batinmustu
