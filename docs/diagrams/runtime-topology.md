# Runtime topology

Snapshot of what is actually running once the local stack is up. Each box
is a process; each arrow is real wire traffic.

```mermaid
flowchart LR
    classDef svc fill:#1e293b,stroke:#334155,color:#f1f5f9
    classDef infra fill:#0f172a,stroke:#1e3a8a,color:#dbeafe
    classDef ext fill:#0f172a,stroke:#475569,color:#cbd5f5,stroke-dasharray:3 3

    subgraph clients[Clients]
      M[Merchant API caller]:::ext
      MB[Merchant webhook receiver]:::ext
    end

    subgraph services[PayFlow services]
      G[Gateway<br/>:5050]:::svc
      I[Identity<br/>:5001]:::svc
      P[Payment<br/>:5002]:::svc
      T[Transaction<br/>:5003]:::svc
      R[Reconciliation<br/>:5004]:::svc
      Rep[Reporting<br/>:5005]:::svc
      N[Notification<br/>:5006]:::svc
      W[Webhooks<br/>:5007]:::svc
    end

    subgraph infra[Infrastructure]
      PG[(Postgres :5433<br/>schemas: identity, payment,<br/>transaction, reconciliation,<br/>reporting, notification, webhooks)]:::infra
      KAF{{Kafka :9092}}:::infra
      RMQ{{RabbitMQ :5672<br/>retry.wait/work/dlq}}:::infra
      RDS[(Redis :6380<br/>idempotency)]:::infra
    end

    subgraph obs[Observability]
      OTEL([OTel Collector<br/>OTLP :4317]):::infra
      JAE([Jaeger :16686]):::infra
      PROM([Prometheus :9090]):::infra
      GRAF([Grafana :3000]):::infra
      SEQ([Seq :5341]):::infra
    end

    M -->|JWT + Idempotency-Key| G
    G --> I
    G --> P
    G --> T
    G --> R
    G --> Rep
    G --> N
    G --> W

    I --> PG
    P --> PG
    T --> PG
    R --> PG
    Rep --> PG
    N --> PG
    W --> PG
    T --> RDS

    T -- HTTP forward user JWT --> P
    R -- HTTP signed service JWT --> P

    T == outbox ==> KAF
    R == outbox ==> KAF
    P == outbox ==> KAF

    KAF == payflow.transaction.* ==> Rep
    KAF == payflow.transaction.captured ==> N
    KAF == payflow.transaction.captured ==> W
    KAF == payflow.refund.requested.v1 ==> R
    KAF == payflow.refund.processing/completed/failed.v1 ==> T
    KAF == payflow.refund.completed/failed.v1 ==> N
    KAF == payflow.refund.completed/failed.v1 ==> W
    KAF == payflow.refund.completed.v1 ==> Rep

    N -- failed sends --> RMQ
    RMQ -. delayed redelivery .-> N
    W -- HMAC-signed POST --> MB

    I -. OTLP .-> OTEL
    P -. OTLP .-> OTEL
    T -. OTLP .-> OTEL
    R -. OTLP .-> OTEL
    Rep -. OTLP .-> OTEL
    N -. OTLP .-> OTEL
    W -. OTLP .-> OTEL
    OTEL -. traces .-> JAE
    PROM -. scrape :8889 .-> OTEL
    GRAF -. datasource .-> PROM

    I -. logs .-> SEQ
    T -. logs .-> SEQ
```

## Reading guide

- **Solid arrows** = synchronous HTTP. The user's JWT flows through the gateway and is either forwarded (TX → Payment) or replaced by a short-lived service JWT (Reconciliation → Payment, where there is no end-user).
- **Double arrows** (`==>`) = outbox → Kafka path. Transaction, Reconciliation, and Payment emit; everyone else only consumes. The outbox publisher claims rows with `SELECT … FOR UPDATE SKIP LOCKED` so multiple instances are safe.
- **Single equals**-style topic arrows show the producer → consumer routing of each topic. A single Kafka message can fan out to three or four consumers (e.g. `payflow.refund.completed.v1` reaches Transaction *and* Reporting *and* Notification *and* Webhooks).
- **Dotted arrows** are telemetry: OTLP traces + metrics into the OTel Collector (single intake), then traces to Jaeger and metrics scraped by Prometheus + visualised in Grafana. Logs ship direct to Seq via Serilog.

## Service summary

| Service        | Owns                                                          | Reads from Kafka (topics)                                                  |
|----------------|---------------------------------------------------------------|----------------------------------------------------------------------------|
| Gateway        | routing, auth boundary                                        | —                                                                          |
| Identity       | tenants, users, JWT issuance                                  | —                                                                          |
| Payment        | provider adapters, charge/refund                              | —                                                                          |
| Transaction    | tx aggregate, refund aggregate                                | `refund.processing/completed/failed`                                       |
| Reconciliation | refund saga, in-process recovery sweeper                      | `refund.requested`                                                         |
| Reporting      | daily summary projection                                      | `transaction.initiated/captured/failed`, `refund.completed`                |
| Notification   | per-event email log, RabbitMQ-backed retry queue + DLQ        | `transaction.captured`, `refund.completed`, `refund.failed`                |
| Webhooks       | merchant subscriptions, HMAC-signed deliveries, retry sweeper | `transaction.captured`, `refund.completed`, `refund.failed`                |
