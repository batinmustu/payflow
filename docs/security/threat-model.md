# Threat Model (STRIDE-lite)

This is a deliberately scoped threat model. We use STRIDE as the frame (Spoofing, Tampering, Repudiation, Information Disclosure, Denial of Service, Elevation of Privilege) and apply it to the parts of PayFlow that matter most: the trust boundaries identified in [c4-context.md](../architecture/c4-context.md).

It is not exhaustive. A real-world payment platform would also have a third-party-conducted threat model with attack trees and quantified likelihood-impact. This document is the engineering team's running checklist.

## Trust boundaries (reminder)

1. **Internet ↔ Gateway.** Any unauthenticated traffic terminates here. Everything inside has a JWT.
2. **Gateway ↔ Service.** Service-to-service over the internal network; internal JWTs are still validated, but the network itself is treated as a softer boundary.
3. **Service ↔ Database.** Per-service credentials, schema-scoped.
4. **Service ↔ Payment Provider.** TLS, signed requests, tenant-scoped credentials.
5. **Service ↔ LLM Provider.** Outbound only; nothing inbound. Treated as an external system that may see data we send it.

## Threats by boundary

### Gateway boundary (Internet → Gateway)

| ID | Threat | Mitigation |
|---|---|---|
| T-G1 | **Spoofing** — request forged with a stolen JWT. | Short access-token lifetime (15m). Refresh-token rotation with reuse detection. Tenant-level denylist available during incidents. |
| T-G2 | **Spoofing** — request forged with a stolen API key. | Keys are hashed at rest with HMAC + server pepper. Revocation is immediate (invalidates Redis cache, propagates via `ApiKeyRevoked` event). |
| T-G3 | **Tampering** — request body modified between client and server. | TLS-only (no plain HTTP). Webhook *outbound* delivery uses HMAC over the body (Stripe-style). |
| T-G4 | **Denial of Service** — flood from one source. | Per-endpoint rate limiting by tenant + IP. 429 responses include `Retry-After`. The gateway has connection limits per source IP. |
| T-G5 | **Repudiation** — "I never made this request". | Every authenticated request is logged with the `sub` claim, IP, trace id, and the audit trail is retained per the data-retention policy. |
| T-G6 | **Information Disclosure** — error responses leak internal information. | Exception handler maps all unhandled exceptions to a generic `INTERNAL_ERROR` with only the trace id. Stack traces are never returned. |

### Service boundary (inter-service)

| ID | Threat | Mitigation |
|---|---|---|
| T-S1 | **Spoofing** — a service impersonates another. | Service-to-service calls use the internal JWT minted by the gateway (or by Identity for background workers). Network controls (K8s NetworkPolicy in prod) restrict which services may call which. |
| T-S2 | **Elevation of Privilege** — a compromised service reads another service's database. | Per-service DB credentials; no service has GRANTs on another's schema (database-per-service rule). |
| T-S3 | **Tampering** — Kafka message tampered in transit. | Brokers run with TLS in prod. mTLS between producer/consumer and broker. |
| T-S4 | **Repudiation** — "this event was never published". | Outbox pattern. Every published event has a `message_id` recorded in the producer's DB; consumers dedupe on it. Reconstruction is possible. |

### Database boundary

| ID | Threat | Mitigation |
|---|---|---|
| T-D1 | **Information Disclosure** — cross-tenant read via a missed query filter. | EF global filters enforced (see [multi-tenancy-isolation.md](../database/multi-tenancy-isolation.md)). Test suite catches missing filter on new entities. RLS as defence-in-depth on the roadmap. |
| T-D2 | **Tampering** — write with wrong `TenantId`. | `MultiTenantSaveChangesInterceptor` validates every added/modified row's `TenantId` against `ITenantContext`. Mismatch throws. |
| T-D3 | **Information Disclosure** — provider credentials read in plaintext from the DB. | `provider_credentials.encrypted_payload` is encrypted with a per-tenant DEK; the DEK is wrapped by a KEK held outside the DB (Vault / KMS in prod, file-based in dev). |
| T-D4 | **Information Disclosure** — backups contain plaintext PII. | Backups are encrypted (Postgres `pgbackrest` with at-rest encryption in prod). Access to backups is logged. |
| T-D5 | **Elevation of Privilege** — SQL injection. | EF parameterised queries. `FromSql`/`ExecuteSqlRaw` forbidden outside read-only projection code; CI lint check enforces. |

### Provider boundary

| ID | Threat | Mitigation |
|---|---|---|
| T-P1 | **Spoofing** — a fake provider response is accepted. | All provider calls are over TLS to pinned hostnames. Webhook callbacks from providers (where used in real prod) verify signatures. |
| T-P2 | **Tampering** — provider response modified in transit. | TLS. |
| T-P3 | **Repudiation** — "we sent the request but you didn't process it". | Every outbound provider call is logged in `adapter_request_log` with the request/response (PII-redacted), plus a duration. |
| T-P4 | **Information Disclosure** — provider credentials leaked. | See T-D3. Credentials are decrypted only in-memory at call time and never logged. |

### LLM boundary

| ID | Threat | Mitigation |
|---|---|---|
| T-L1 | **Information Disclosure** — PII sent to the LLM provider. | Prompt template strips identifiers. Retrieved chunks from the global KB never contain PII by construction. Tenant-scoped chunks have been ingested *by us* from explicitly-shareable sources (we control what goes in, no PII allowed). |
| T-L2 | **Prompt injection** — user message instructs the model to ignore system instructions. | The system prompt is firm and repeated; the model is instructed to treat user messages as data, not instructions. We do not give the model the ability to call tools or change state, so the worst outcome of a successful injection is a misleading answer, not an action. |
| T-L3 | **Spoofing** — the LLM provider impersonated. | TLS to the documented hostnames. Provider SDKs handle the verification. |

## Cross-cutting threats

### Webhook signature bypass

A merchant whose verification is missing or weak accepts any POST claiming to come from PayFlow. We cannot fix the merchant's code, but we make it as easy as possible to verify correctly: the [webhook spec](../api/webhooks.md) includes a working code sample, the secret is shown once, and the dashboard surfaces a "test webhook" tool.

### Replay attacks on signed payloads

Webhook signatures include a timestamp; we reject `t > 5min old`. Replays inside that window are possible — but the merchant should dedupe on `event_id`, which makes a replayed message a no-op.

### Compromised refresh token

Handled by the reuse-detection design on refresh tokens. A stolen-and-replayed refresh token revokes the entire session family.

### Insider threat (support agent with elevated permissions)

Support reads through the same dashboard with a system-level role that allows cross-tenant reads. Every cross-tenant read is logged with the support agent's `sub` claim, the target tenant, and the entity touched. The audit log is retained for 7 years.

### Stolen developer laptop

`.env` files are gitignored and contain only dev credentials with no production reach. Production credentials live in K8s secrets / a secrets manager; no developer machine has them.

## Risks we accept

- **The 15-minute access-token window.** A stolen access token works until it expires. The cost of shorter lifetimes (more refreshes, more Identity load, worse UX) was judged higher than the residual risk. The kill-switch denylist mitigates incident response.
- **Cross-service IDs are not enforced by foreign keys.** A bug in the producer can publish events with `transaction_id` values that don't exist. We accept this in exchange for service autonomy. The reconciliation flow catches the most consequential case (missing payments).
- **The mock providers are not real.** A reviewer should know that real-provider integration introduces more failure modes (notably webhook authenticity verification and 3DS callbacks). The adapter layout is designed so each provider's authentication strategy lives in its own adapter file, ready to be hardened.

## What an attacker would actually try

If I were attacking PayFlow as it stands, in order of likely value:

1. **Cross-tenant data leak.** A missing query filter or a creative `IgnoreQueryFilters()` call. The test suite is the main defence. Manual code review on every new entity is the secondary defence.
2. **API key extraction from a merchant.** Outside our perimeter, but our response (immediate revocation, fast event propagation to the gateway cache) determines the blast radius.
3. **Idempotency-key bypass for retries.** If the dedup store could be made to drop a key, a duplicate transaction would slip through. Mitigated by making the middleware fail closed (reject on Redis error).
4. **Prompt injection against the AI assistant.** Annoying but not catastrophic — the assistant has no write access. The worst case is bad advice to a tenant user.

Periodic review (at minimum every major release) revisits these. This file is the running checklist.
