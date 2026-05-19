# ADR-0005: `ILlmProvider` abstraction over OpenAI and Anthropic

- **Status:** Accepted
- **Date:** 2026-03-08
- **Deciders:** Tech lead (solo)

## Context

The AI Assistant service does two things that depend on an external LLM:

1. **Generate embeddings** for ingested documents and for user queries (RAG).
2. **Stream chat completions** with a retrieved-context prompt.

There are two viable hosted providers we care about for this project (OpenAI and Anthropic). Their APIs differ in request shape, streaming protocol, token-counting rules, error semantics, and how they expose "system" vs "user" messages.

Hard-coding one provider is convenient but fragile: a single rate-limit incident, a pricing change, or a model deprecation forces a rewrite. Designing for both from the start costs more up front but matches the value-add of this service.

## Decision

Introduce an `ILlmProvider` abstraction with two implementations:

```csharp
public interface ILlmProvider
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
    IAsyncEnumerable<CompletionChunk> StreamCompletionAsync(
        CompletionRequest request,
        CancellationToken ct);
}
```

- `CompletionRequest` is the abstract shape: system prompt, messages, model hint (logical, not provider-specific), max tokens, temperature, optional stop sequences.
- `CompletionChunk` is what each implementation yields: a text delta, a usage update at the end, and an optional finish reason.
- Each implementation owns its provider-specific quirks: OpenAI's `functions`/`tools` array vs Anthropic's content blocks, SSE event-name differences, retry classification.
- Provider selection is per-tenant configuration. The factory reads the configured provider name and returns the matching implementation; nothing above the factory cares which one is in use.

Embedding generation, in practice, will use a single provider regardless of completion choice (we standardise on OpenAI `text-embedding-3-small` for now). The interface still exposes `EmbedAsync` because we may revisit this when Anthropic ships first-class embeddings.

## Consequences

### Positive

- Pricing or rate-limit surprise on one provider has a defined response (switch tenant config, re-deploy, observe).
- The prompt-template layer (which is where prompt engineering actually lives) does not branch on provider. It builds a `CompletionRequest` and hands it off.
- Testing is straightforward: a mock `ILlmProvider` is the only thing needed to exercise the RAG pipeline end-to-end without external dependencies.
- Cost accounting can be done at the abstraction layer: a decorator around `ILlmProvider` records token usage, latency, and failure rates per provider per tenant.

### Negative

- **The abstraction is a lowest-common-denominator.** Provider-specific features (Claude's tool use, OpenAI's structured outputs) sit awkwardly. We address this by exposing them through targeted extension methods on the provider implementations, not on the interface — callers that need them opt into a provider-specific path consciously.
- **Streaming protocol differences are hidden.** SSE vs Anthropic's event stream are different on the wire. The abstraction normalises to an `IAsyncEnumerable<CompletionChunk>`. This is the right shape but it does mean clients lose visibility into the underlying transport.
- **Two SDKs to keep current.** Mitigated by infrequent release cadence in the AI assistant service.

## Alternatives considered

### Single provider, deferred abstraction

The YAGNI choice. Reasonable for an MVP. Rejected because: (a) the abstraction is small, the leverage is high; (b) the project's premise includes demonstrating the Strategy pattern (already used in payment provider adapters), and this is the natural second instance.

### A higher-level abstraction (a "conversation runner")

Hide more behind the interface — context retrieval, prompt assembly, evaluation. Rejected. The right level of abstraction here is "talk to an LLM"; bundling RAG into the interface would couple it to retrieval choices that are likely to change independently of provider choice.

### LiteLLM or similar third-party gateway

A library that abstracts many LLM providers behind a single API. Considered. Rejected because:

- Adding a dependency to abstract two providers we are already going to support directly is not a good trade.
- It would mean importing concepts from outside our model into the AI service.
- We lose visibility into provider-specific behaviour exactly when we most need it (debugging a streaming failure).

## Notes

The pattern intentionally mirrors `IPaymentProvider` in the payment service. Two strategies sitting behind a clean abstraction in two unrelated parts of the system is exactly what we want — it shows the pattern is principled, not coincidental.

The provider abstraction does not solve evaluation. Whether the LLM gave a *good* answer is a different concern, addressed (lightly, in this reference implementation) inside the RAG architecture doc. See [docs/ai/rag-architecture.md](../ai/rag-architecture.md).
