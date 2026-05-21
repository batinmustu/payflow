# C4 — System Context Diagram (Level 1)

The Level 1 view shows PayFlow as a single box and the people and systems it talks to. The interesting question at this level is "who depends on PayFlow and what does PayFlow depend on" — not what is inside.

```mermaid
flowchart TB
    classDef person fill:#e8f1ff,stroke:#2257b8,color:#000
    classDef system fill:#1168bd,stroke:#0b4884,color:#fff
    classDef external fill:#999,stroke:#666,color:#fff

    M_ADMIN["Merchant Admin<br/><i>uses dashboard</i>"]:::person
    M_DEV["Merchant Integrator<br/><i>calls API from backend</i>"]:::person
    END_USER["End User (Cardholder)<br/><i>pays on merchant site</i>"]:::person
    SUPPORT["PayFlow Support<br/><i>read-only access</i>"]:::person

    PAYFLOW["PayFlow<br/>Payment orchestration<br/>and reconciliation platform"]:::system

    IYZ["Iyzico<br/><i>Payment provider</i>"]:::external
    STR["Stripe<br/><i>Payment provider</i>"]:::external
    PPL["PayPal<br/><i>Payment provider</i>"]:::external
    LLM["LLM API<br/><i>OpenAI / Anthropic</i>"]:::external
    SMTP["Email gateway<br/><i>SMTP</i>"]:::external
    SMS["SMS gateway"]:::external

    M_ADMIN -->|"Manages tenant config,<br/>views reports, refunds"| PAYFLOW
    M_DEV -->|"Creates transactions,<br/>receives webhooks"| PAYFLOW
    END_USER -.->|"Pays on merchant site<br/>(merchant calls PayFlow)"| PAYFLOW
    SUPPORT -->|"Investigates issues"| PAYFLOW

    PAYFLOW -->|"Authorise / capture /<br/>refund / fetch statement"| IYZ
    PAYFLOW -->|"Authorise / capture /<br/>refund / fetch statement"| STR
    PAYFLOW -->|"Authorise / capture /<br/>refund / fetch statement"| PPL
    PAYFLOW -->|"Embed text,<br/>generate completions"| LLM
    PAYFLOW -->|"Deliver transactional<br/>email"| SMTP
    PAYFLOW -->|"Deliver SMS"| SMS

    PAYFLOW -.->|"Webhook events"| M_DEV
```

## Actors and external systems

### Merchant Admin

A human user logging into the PayFlow dashboard. Belongs to exactly one tenant. Typical actions: register a payment provider, view reports, issue a refund, configure routing rules.

### Merchant Integrator

A developer at the merchant's company. They never touch the dashboard for routine work; they call `POST /api/transactions` from their backend and consume webhooks. The API surface is their primary interface to PayFlow.

### End User (cardholder)

The shopper paying on the merchant's site. They do not interact with PayFlow directly — the merchant's checkout collects card details via a provider's tokenisation widget (or the merchant's own PCI-scoped vault) and passes us a token. The reason we include them in the context diagram at all is to make clear that the data path crosses three trust boundaries (browser → merchant → PayFlow → provider) and our threat model has to consider all of them.

### PayFlow Support

Internal staff with read-only access scoped per ticket. Their tooling is not separate; they log into the dashboard with elevated permissions and the audit log records every read.

### Payment providers (Iyzico, Stripe, PayPal)

External systems that actually move money. In this reference implementation they are mocked, but the contracts are modelled after the real APIs. PayFlow communicates with them through one provider adapter each. They communicate back to PayFlow through:

- synchronous HTTPS responses for authorise / capture / refund calls,
- daily settlement statement files,
- (in real life, also asynchronous webhooks for 3DS callbacks and disputes — out of scope here).

### LLM API (OpenAI / Anthropic)

The AI assistant service calls either OpenAI or Anthropic depending on configuration. Embeddings are produced by OpenAI's `text-embedding-3-small` and completions by whichever provider is selected.

### Email gateway (SMTP) and SMS gateway

Outbound transactional channels. Both are mocked in dev. The notification service is the only consumer.

## Trust boundaries

Three are worth naming at this level:

1. **Internet ↔ PayFlow.** All inbound traffic terminates at the gateway. JWT validation and rate limiting happen here.
2. **PayFlow ↔ payment providers.** Each adapter holds tenant-specific credentials. The threat model treats the providers as trusted-but-fallible.
3. **PayFlow ↔ LLM provider.** Treated as a third party with no access to PII. The prompt-building layer strips identifiers before sending; tenant data that reaches the LLM is aggregated, never row-level personal data.

## What this diagram does not show

- Internal services (gateway, identity, payment, transaction, reconciliation, notification, reporting, webhooks, AI assistant) — those are Level 2 in [c4-container.md](c4-container.md).
- Observability sinks (OTel Collector, Jaeger, Prometheus, Grafana, Seq) — those are infrastructure, not actors.
- The merchant's own infrastructure beyond "their backend calls our API" — we treat it as a black box.
