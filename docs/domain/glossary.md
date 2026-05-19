# Ubiquitous Language

This is the shared vocabulary used across PayFlow's code, docs, API contracts and conversations. If a term is here, use it as written — same spelling, same meaning. If a term is *not* here and you find yourself reaching for one, propose it (PR to this file) before sprinkling it through the code.

The list is grouped by bounded context, not alphabetically. Reading it top-to-bottom should give a rough mental model of the system.

## How to read this file

- Each entry has a definition and the **owning context** (the service whose code is the source of truth for the term).
- Where two terms look similar but mean different things, we keep them next to each other and explicitly disambiguate. The most important pair is *Transaction vs Payment*.
- A short "Terms we avoid" section at the end lists words deliberately kept out of the codebase, with the reason.

---

## Tenant & Merchant — *Identity context*

| Term | Definition |
|---|---|
| **Tenant** | A logically isolated customer of PayFlow. Every row in every business table carries a `TenantId`. A tenant maps 1:1 to a merchant onboarding agreement, never shared across customers. |
| **Merchant** | The business-side identity *within* a tenant. In practice a tenant has exactly one merchant today; the separation exists so we can later support marketplaces (one tenant, many sub-merchants) without a schema migration. |
| **Tenant User** | A person logging in to a tenant's dashboard. Has one or more roles inside that tenant. The same email may belong to users of different tenants — uniqueness is `(TenantId, Email)`, not `Email`. |
| **API Key** | Long-lived credential used by a merchant's server-to-server integration. Issued per tenant. Stored hashed; the plaintext is shown exactly once at creation time. |
| **Provider Credentials** | The keys/secrets a tenant has registered for each payment provider (e.g. Iyzico API key + secret). Encrypted at rest with a per-tenant data key. |

---

## Payment Provider — *Payment context*

| Term | Definition |
|---|---|
| **Payment Provider** | An external system that actually moves the money (Iyzico, Stripe, PayPal). Each one has its own protocol, error codes, and quirks. |
| **Provider Adapter** | Our code that translates between PayFlow's internal model and a single provider's protocol. One adapter per provider. |
| **Authorization** (auth) | A successful "hold" placed on a card. No funds move yet. Has its own provider-issued reference. |
| **Capture** | The follow-up step that turns an authorization into an actual fund movement. Can be partial. |
| **Sale** | Auth + capture in one call. Used when funds should move immediately (most card-not-present flows). |
| **Void** | Cancellation of an authorization *before* capture. Free; releases the hold. |
| **Refund** | Reversal of a capture. Can be full or partial, and can happen days or months after the capture. Refund is not a void — they go through different provider endpoints and have different settlement implications. |
| **3-D Secure (3DS)** | The card-issuer authentication step that hands the cardholder off to their bank's challenge page. Optional per provider, mandatory in some regions for some BIN ranges. |
| **Settlement** | The next-day (typically T+1 or T+2) event where the provider actually moves funds into the merchant's bank account. Different from capture. |
| **Daily Limit** | A per-tenant, per-provider ceiling on the total amount captured in a UTC day. Used by the routing engine to fail over when a provider runs out. |

---

## Transaction — *Transaction context*

This is the bounded context that owns the lifecycle of "an attempt to take money". The naming here is the part most likely to cause arguments, so read carefully.

| Term | Definition |
|---|---|
| **Transaction** | The **internal aggregate** owned by the Transaction service. One transaction per business-level payment attempt. Carries the state machine (Initiated → Authorized → Captured → …). Has a stable `TransactionId` that the merchant sees in dashboards and uses for refunds. A single Transaction may have *zero or more* underlying Payment attempts against providers (because of failover). |
| **Payment** *(in this codebase)* | A **single round-trip** with a specific provider through a specific adapter. Owned by the Payment service. A Payment has the provider-issued reference and the raw response. Transactions own Payments, not the other way around. |
| **Charge** | We do **not** use this word. See "Terms we avoid". |
| **Order Reference** | A merchant-supplied string (their internal order ID). We do not interpret it; we store it and echo it back in webhooks. Used as part of the idempotency strategy. |
| **Idempotency Key** | Client-supplied header (`Idempotency-Key`) on transaction-creating endpoints. Scoped to `(TenantId, Endpoint, Key)`. TTL: 24 hours. |
| **Intelligent Routing** | The decision logic that picks which Provider Adapter to try first, and which one to fall back to. Inputs: tenant preference order, daily limit headroom, current provider health. |
| **Soft Decline** | A provider refusal that is plausibly retryable on another provider (insufficient funds, do-not-honor, provider rate limit). Triggers failover. |
| **Hard Decline** | A provider refusal that retrying elsewhere will not fix (invalid card, fraud, 3DS failure). Does *not* trigger failover. |
| **Routing Rule** | The tenant-level configuration that lists providers in preference order plus the per-provider daily caps. |

> If you take one thing away from this file: **Transaction is ours; Payment is per-provider. A failed Payment does not fail the Transaction — only an exhausted routing list does.**

---

## Reconciliation — *Reconciliation context*

| Term | Definition |
|---|---|
| **Statement** | The daily report (usually CSV) a provider issues listing every captured transaction it settled that day. Fetched by a scheduled job. |
| **Reconciliation Run** | One execution of the daily job that compares a Statement against our Transactions. Has a status, a date, a provider, and a list of mismatches. |
| **Mismatch** | A row that exists on one side but not the other, or exists on both with a different amount. Mismatches are flagged, not auto-resolved. |
| **Settlement Date** | The date the provider attributes the funds to (their banking-day calendar). Not always the same as our capture date because of timezone and cutoff differences. |

---

## Events & Messaging — *cross-cutting*

| Term | Definition |
|---|---|
| **Domain Event** | An in-process event raised by an aggregate after a state change. Stays inside the service that produced it. Never published to Kafka directly. |
| **Integration Event** | A versioned, cross-service event published to Kafka. Built by mapping one or more Domain Events. The contract lives in `PayFlow.Contracts` and is treated as a public API. |
| **Outbox Message** | A row in the `outbox_messages` table written in the same DB transaction as the business state change. A background worker reads from here and publishes the corresponding Integration Event to Kafka. The mechanism that makes "publish on commit" actually safe. |
| **Saga** | A multi-step cross-service workflow coordinated through Integration Events (choreography, not orchestration). Refunds are the main saga. |
| **Correlation ID** | An ID that follows a single business operation across all services and messages. Lives in the `traceparent` HTTP header and in Kafka message headers. |
| **Causation ID** | The ID of the message that *caused* the current message. Lets us reconstruct event chains for audit. |

---

## AI Assistant — *AI Assistant context*

| Term | Definition |
|---|---|
| **Conversation** | A persistent chat thread between a tenant user and the assistant. Has a `TenantId`; never shared across tenants. |
| **Message** | One turn in a conversation. Either `user`, `assistant`, or `system`. |
| **Embedding** | The vector representation of a chunk of text. Stored in pgvector. |
| **Chunk** | A bounded slice of a source document (~500 tokens, with overlap). The retrieval unit. |
| **Retrieval / Top-K** | The vector similarity step that pulls the K most relevant chunks for a query. |
| **Citation** | An identifier (chunk ID + source document) returned alongside an assistant message so a user can verify what was used to answer. |
| **LLM Token** | A unit of model input/output billing. Not to be confused with auth tokens — always say "LLM token" when ambiguity is possible. |

---

## Cross-cutting

| Term | Definition |
|---|---|
| **Read Model / Projection** | A denormalised table populated by consuming Integration Events, used by the Reporting service for fast queries. Eventually consistent. |
| **Money / Minor Unit** | All amounts are stored as integers in the minor unit of the currency (kuruş for TRY, cents for USD). The pair `(Amount, Currency)` is a value object; raw decimals are forbidden. |
| **Webhook** | An outbound HTTP POST from PayFlow to a merchant URL, signed with an HMAC over the body. Has its own retry schedule and DLQ. |
| **Trace ID** | The W3C trace context value propagated across HTTP and Kafka. Synonym of Correlation ID in our codebase. |

---

## Terms we deliberately avoid

| Term | Why it's banned | Use instead |
|---|---|---|
| **Charge** | Ambiguous — it can mean "authorize", "capture", or "sale" depending on the speaker's provider background. We can't stop external docs from using it, but our code, API and tickets must not. | "Authorization", "Capture" or "Sale" — pick the precise one. |
| **Order** | Belongs to the *merchant's* business domain. We never model the order itself, only its reference. | "Order Reference" for the string; "Transaction" for what we own. |
| **Customer** | Overloaded. Sometimes it's the tenant (our paying customer), sometimes the end shopper (the tenant's customer). | "Tenant" or "End User" — be explicit. |
| **Cancel** | Different things to authorization (Void) and to capture (Refund). | "Void" or "Refund". |
| **Payment** *as a synonym for Transaction* | We use Payment for the per-provider attempt. Calling the top-level entity Payment too would collapse the most important distinction in this codebase. | "Transaction" for the lifecycle entity. |

---

## Notes

- Currency codes follow ISO 4217 (`TRY`, `USD`, `EUR`). The internal name for the currency field is always `Currency`, never `CurrencyCode`.
- Anywhere a "user" appears without a qualifier, the tenant scope is implicit — there is no global user. The auth middleware will throw before any handler sees a user without a `TenantId` claim.
- KVKK/GDPR-sensitive fields are marked `[PII]` in the ERD docs. The handling rules for `[PII]` columns are noted alongside the column in the owning ERD; retention windows are intentionally out of scope for this reference implementation and would land in their own doc on a real production rollout.

This file is expected to grow. Add an entry the first time a new concept becomes load-bearing in a discussion; don't wait until it's already two weeks into the code.
