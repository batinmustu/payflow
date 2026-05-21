# Local Development Stack

How to bring up PayFlow on a developer machine. Goal: clone, run two commands, hit the dashboard. Anything more is friction we want to remove.

## Prerequisites

- Docker Desktop (or equivalent: OrbStack, Podman with Docker compatibility)
- .NET 8 SDK
- A reasonably modern machine — 16GB RAM is comfortable, 8GB will work if you turn off the AI assistant

## Bring it up

```sh
git clone https://github.com/<owner>/payflow.git
cd payflow

# Infrastructure (Postgres, Redis, Kafka, RabbitMQ, Jaeger, Seq, OTel
# Collector, Prometheus, Grafana)
docker compose -f deploy/docker-compose.infra.yml up -d

# Services (gateway + 7 application services + their in-process workers)
docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d
```

That's it. The dashboard is at `http://localhost:5050`. When running services on the host (see [below](#running-services-on-the-host-faster-iteration)) the per-service OpenAPI explorers are at `http://localhost:5001..5007/swagger`.

The first run pulls images and applies migrations; expect 2-3 minutes. Subsequent runs are seconds.

## Compose files

We split into two files on purpose:

- **`docker-compose.infra.yml`** — the long-lived dependencies (Postgres, Kafka, RabbitMQ, observability stack). Started once, kept running. Pulls less frequently.
- **`docker-compose.prod.yml`** — the application services + their copy of the infrastructure (so the whole stack is reproducible from one file when you want a prod-shaped deploy). Rebuilt every time you `docker compose build` after a code change.

This split means you can leave Kafka and Postgres running between sessions and only rebuild what you change. For day-to-day dev work the **infra-only** compose plus `dotnet run` on the host (see [below](#running-services-on-the-host-faster-iteration)) is the fastest loop; the prod-shaped file is mainly for image/CI verification.

## What's in `docker-compose.infra.yml`

| Service | Port | Notes |
|---|---|---|
| `postgres` | 5433 (host) → 5432 (container) | Single instance, with `pgvector` extension. Database `payflow` with one schema per service (`identity`, `payment`, `transaction`, `reconciliation`, `reporting`, `notification`, `webhooks`). Host port is 5433 so a developer's native Postgres on 5432 keeps working. |
| `redis` | 6380 (host) → 6379 (container) | No persistence in dev. Host port is 6380 so a developer's native Redis on 6379 keeps working — same shape as the postgres swap to 5433. |
| `kafka` | 9092 | Single broker; auto-create topics enabled in dev only. KRaft mode (no ZooKeeper). |
| `rabbitmq` | 5672, management 15672 | Default guest/guest credentials in dev. |
| `jaeger` | UI 16686 | Memory backend; traces lost on restart. OTLP ingestion now happens via the OTel Collector. |
| `otel-collector` | OTLP 4317/4318, Prom 8889 | Single OTLP intake for every PayFlow service. Forwards traces to Jaeger, exposes metrics for Prometheus to scrape. |
| `prometheus` | 9090 | Scrapes the collector every 15s. 24h local retention. |
| `grafana` | 3000 | UI at `localhost:3000` (admin/admin). Dashboards auto-provisioned from `deploy/observability/grafana/dashboards/`. |
| `seq` | 5341 | Log aggregator. Web UI at `localhost:5341`. |
| `iyzico-mock` | 9001 | Mock Iyzico API. |
| `stripe-mock` | 9002 | Mock Stripe API. |
| `paypal-mock` | 9003 | Mock PayPal API. |

## Application services (in `docker-compose.prod.yml` and `dotnet run`)

The seven application services share the same port assignment whether you run them in compose or with `dotnet run --urls=…` on the host. Only the gateway is exposed publicly; the rest are reachable on `localhost:<port>` when run on the host, or only over the docker network when in compose.

| Service | Port | What |
|---|---|---|
| `gateway` | 5050 | YARP reverse proxy. The only port the host normally needs to expose. Picked over 5000 because macOS uses 5000 for AirPlay Receiver. |
| `identity` | 5001 | JWT issuer, tenants/users/roles. |
| `payment` | 5002 | Provider adapters behind a common `IPaymentProvider`. |
| `transaction` | 5003 | Hosts the outbox publisher in-process. |
| `reconciliation` | 5004 | Refund saga consumer + recovery worker (in-process `BackgroundService`). |
| `reporting` | 5005 | CQRS read-side; consumes Kafka, serves dashboard queries. |
| `notification` | 5006 | Kafka consumers + RabbitMQ retry consumer (in-process). |
| `webhooks` | 5007 | Kafka consumers + HMAC-signed merchant deliveries + retry sweeper. |
| `ai-assistant` | 5008 | Planned (M7). SSE-friendly; no buffering. |

In dev the services run with `ASPNETCORE_ENVIRONMENT=Development`, which enables Swagger UIs and verbose error pages.

## First-time setup

On first run, `docker compose up` triggers:

1. Each service's startup migrations apply. Idempotent — re-runs are no-ops.
2. Identity seeds the system roles (`admin`, `developer`, `viewer`) and a demo tenant + admin user (`demo@payflow.local` / `demo`).
3. Payment seeds the three provider definitions and registers mock-provider credentials for the demo tenant.
4. AI Assistant runs the initial ingestion job over `docs/` (so the assistant can answer questions about itself).
5. Kafka topics get created on first publish.

After this, `localhost:5050` should show a working login form.

## Logging in (dev)

Pre-seeded credentials are in `deploy/seed-data/`. The demo tenant is `demo` and the admin password is `demo`. (Yes, it is `demo` / `demo`. Yes, this is dev only. The seed code is gated on the environment.)

For API integration testing, the demo tenant ships with a long-lived dev API key in `deploy/seed-data/api-keys/dev-key.txt`. Never commit a key with this name from a real environment.

## Running services on the host (faster iteration)

Sometimes Docker-rebuild-per-change is slower than you want. The workflow:

```sh
# infrastructure stays in docker
docker compose -f deploy/docker-compose.infra.yml up -d

# the service you're working on runs on the host
cd src/Services/Transaction/PayFlow.Transaction.API
dotnet run
```

The host service uses the same `localhost:<port>` URLs for infra. Other services keep running in Docker against the same infra. Connection strings in `appsettings.Development.json` reference `localhost:5432` etc., which works from both inside and outside Docker (the infra ports are mapped to host).

## Bringing it down

```sh
# stop services but keep infra running (faster restarts)
docker compose down

# stop everything including infra (frees memory)
docker compose -f deploy/docker-compose.infra.yml down

# also wipe data volumes (start completely fresh)
docker compose -f deploy/docker-compose.infra.yml down -v
```

The `-v` form wipes Postgres data. Use this when migrations break in a way that's faster to redo than to fix in place.

## Common issues

**"Port 5432 already in use" / `psql` connects to the wrong server.** Postgres in this stack is published on the host as **5433** for exactly this reason — a developer's native Postgres can keep running on 5432 without clashing. Connect to ours with `psql -h localhost -p 5433 -U payflow`. Services that run inside the docker network still use 5432; that's the in-container port.

**"redis-cli is empty even though the app set keys" / `KEYS *` returns nothing on payflow-redis.** A native Redis bound to `127.0.0.1:6379` wins over docker's wildcard `*:6379` for host-side connections. This stack publishes Redis on **6380** for the same reason as Postgres; connect with `redis-cli -p 6380` (or `docker exec payflow-redis redis-cli` for the in-container side). If a service's `RedisConnectionString` ever points at 6379, it is hitting the developer's own Redis, not ours.

**"403 Forbidden" or "access denied" on `localhost:5000`.** On macOS, port 5000 is owned by the AirPlay Receiver system service (`ControlCenter` process). The gateway is on 5050 to dodge this; if you see 5000 anywhere in your local config, it's stale. Disabling AirPlay Receiver in System Settings → General → AirDrop & Handoff is the alternative, but we prefer the port change so the project works without OS-level tweaks.

**"Kafka connection refused" from a service.** Kafka takes ~15s to come up. The services retry on startup; if the wait isn't long enough, the startup probe fails and the service stays in `Created`. Wait a few seconds and `docker compose start` the failed service.

**Migrations diverged.** If `dotnet ef migrations add` produced a migration that conflicts with what's in Docker, the easiest path is to `docker compose -f deploy/docker-compose.infra.yml down -v` and start over. Local dev does not preserve data across schema changes.

**Slow first-time build.** The image cache is empty. Subsequent builds reuse layers. Don't optimise this until you've tried the cached path.

## What you don't need to do

- **Run migrations manually.** Services run migrations on startup. You can opt out via env vars in CI, but in dev it's automatic.
- **Create Kafka topics manually.** Auto-create is on in dev.
- **Configure secrets.** Dev secrets are committed (gitignored only for prod credentials). The demo tenant works out of the box.
- **Hit infrastructure directly.** Everything routes through the gateway. The Swagger UIs of individual services are exposed for development convenience; don't write integration tests against them.

## Where to look when things are off

- Logs: `docker compose logs -f <service>` or Seq at `localhost:5341`.
- Traces: Jaeger at `localhost:16686`.
- Metrics: Grafana at `localhost:3000` (admin/admin) — the **PayFlow — Service overview** dashboard is auto-loaded; Prometheus raw queries at `localhost:9090`.
- Database: `psql -h localhost -U payflow -d payflow` (password in `docker-compose.infra.yml`).
- Kafka topics: `docker compose exec kafka kafka-topics.sh --bootstrap-server localhost:9092 --list`.
- RabbitMQ retry queues: management UI at `localhost:15672` (guest/guest); `payflow.notification.retry.{wait,work,dlq}`.

For anything else: this doc, then the service-level READMEs, then ask. The first answer for "where is X documented" is the `docs/` folder.
