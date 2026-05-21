# k6 load test scenarios

Reproducible load profiles for PayFlow. Each scenario is a single `.js`
file that goes through the gateway — no scenario hits a downstream service
directly, so the load profile matches what real clients experience.

## Prerequisites

- [k6](https://k6.io/docs/getting-started/installation/) — `brew install k6` on macOS, or grab the binary
- A running PayFlow stack:
  ```sh
  docker compose -f deploy/docker-compose.infra.yml up -d
  docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d
  ```
- Identity is seeded with the demo tenant (`demo` / `demo@payflow.local` / `demo`).
  Override via `TENANT_SLUG=...`, `ADMIN_EMAIL=...`, `ADMIN_PASSWORD=...`.

## Scenarios

| Scenario | What it does | When to run it |
|----------|--------------|----------------|
| `smoke.js` | 3 VUs × 30s of health probes. Zero failures allowed. | On every PR / pre-deploy. |
| `transaction-happy-path.js` | Constant arrival rate of `POST /api/transactions`. JWT cached per VU. | Regression check for the Transaction → Payment hop. |
| `refund-saga.js` | Create transaction + request refund per iteration. Drives the saga. | Verify Reconciliation consumer keeps up; watch the Grafana saga panels. |

## Quick start

```sh
# Smoke — runs in ~30s
k6 run deploy/k6/scenarios/smoke.js

# Transaction load — 20 req/s for 1 minute (defaults)
k6 run deploy/k6/scenarios/transaction-happy-path.js

# Crank it up
K6_TARGET_RPS=100 K6_RAMP_MINUTES=5 k6 run deploy/k6/scenarios/transaction-happy-path.js

# Refund saga — 5 flows/s for 2 minutes
k6 run deploy/k6/scenarios/refund-saga.js
```

## Watching the load

Open the Grafana dashboard at <http://localhost:3000> while a scenario runs.
Filter the `service` template variable to `payflow-transaction` and
`payflow-payment` for the happy-path scenario, add `payflow-reconciliation`
and `payflow-notification` for the refund saga.

## Environment overrides

| Variable | Default | Notes |
|----------|---------|-------|
| `BASE_URL` | `http://localhost:5050` | Gateway URL. |
| `TENANT_SLUG` | `demo` | |
| `ADMIN_EMAIL` | `demo@payflow.local` | |
| `ADMIN_PASSWORD` | `demo` | |
| `CARD_TOKEN` | `tok_test_visa` | Card token consumed by Payment's stub providers. |
| `CURRENCY` | `TRY` | |
| `K6_TARGET_RPS` | scenario-specific | Constant-arrival-rate target. |
| `K6_RAMP_MINUTES` | 1 | (happy-path) Duration. |
| `K6_DURATION` | `2m` | (refund-saga) Duration as a k6 timestring. |

## Thresholds

Each scenario defines pass/fail SLOs as k6 thresholds. A failed threshold
makes the k6 process exit non-zero, which is what CI consumes. The current
defaults are deliberately loose — tune them down once you have a baseline
from production traffic.
