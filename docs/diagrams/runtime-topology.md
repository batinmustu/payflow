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
    end

    subgraph services[PayFlow services]
      G[Gateway<br/>:5050]:::svc
      I[Identity<br/>:5001]:::svc
      P[Payment<br/>:5002]:::svc
      T[Transaction<br/>:5003]:::svc
      R[Reconciliation<br/>:5004]:::svc
      Rep[Reporting<br/>:5005]:::svc
      N[Notification<br/>:5006]:::svc
    end

    subgraph infra[Infrastructure]
      PG[(Postgres :5433<br/>schemas: identity, transaction,<br/>reconciliation, report, notification)]:::infra
      KAF{{Kafka :9092}}:::infra
      RDS[(Redis :6380<br/>idempotency)]:::infra
      JAE([Jaeger :16686<br/>OTLP :4317]):::infra
      SEQ([Seq :5341]):::infra
    end

    M -->|JWT + Idempotency-Key| G
    G --> I
    G --> P
    G --> T
    G --> R
    G --> Rep
    G --> N

    I --> PG
    P --> PG
    T --> PG
    R --> PG
    Rep --> PG
    N --> PG
    T --> RDS

    T -- HTTP forward user JWT --> P
    R -- HTTP signed service JWT --> P

    T == outbox ==> KAF
    R == outbox ==> KAF
    KAF == payflow.transaction.* ==> Rep
    KAF == payflow.transaction.* ==> N
    KAF == payflow.refund.requested.v1 ==> R
    KAF == payflow.refund.processing/completed/failed.v1 ==> T
    KAF == payflow.refund.completed/failed.v1 ==> N
    KAF == payflow.refund.completed.v1 ==> Rep

    I -. OTLP .-> JAE
    P -. OTLP .-> JAE
    T -. OTLP .-> JAE
    R -. OTLP .-> JAE
    Rep -. OTLP .-> JAE
    N -. OTLP .-> JAE
    I -. logs .-> SEQ
    T -. logs .-> SEQ
```

## Reading guide

- **Solid arrows** = synchronous HTTP. The user's JWT flows through the gateway and is either forwarded (TX → Payment) or replaced by a short-lived service JWT (Reconciliation → Payment, where there is no end-user).
- **Double arrows** (`==>`) = outbox → Kafka path. Transaction and Reconciliation emit; everyone else only consumes. The outbox publisher claims rows with `SELECT … FOR UPDATE SKIP LOCKED` so multiple instances are safe.
- **Single equals**-style topic arrows show the producer → consumer routing of each topic. A single Kafka message can fan out to two or three consumers (e.g. `payflow.refund.completed.v1` reaches Transaction *and* Reporting *and* Notification).
- **Dotted arrows** are telemetry: OTLP to Jaeger for traces, Seq for structured logs. Both run in the docker compose stack.

## Service summary

| Service        | Owns                                | Reads from Kafka (topics)                                                                 |
|----------------|-------------------------------------|-------------------------------------------------------------------------------------------|
| Gateway        | routing, auth boundary              | —                                                                                          |
| Identity       | tenants, users, JWT issuance        | —                                                                                          |
| Payment        | provider adapters, charge/refund    | —                                                                                          |
| Transaction    | tx aggregate, refund aggregate      | `refund.processing/completed/failed`                                                       |
| Reconciliation | refund saga                         | `refund.requested`                                                                         |
| Reporting      | daily summary projection            | `transaction.initiated/captured/failed`, `refund.completed`                                |
| Notification   | per-event email log                 | `transaction.captured`, `refund.completed`, `refund.failed`                                |
