# Coding Standards

C# and project conventions specific to PayFlow. The defaults from Microsoft's coding guidelines are assumed; this document covers what we do *differently* or *more specifically*.

The goal is not to argue with developers about taste — it is to keep the codebase legible to someone joining cold.

## Project layout per service

```
Services/<Name>/
├── PayFlow.<Name>.Domain/         # Aggregates, value objects, domain events. No external dependencies.
├── PayFlow.<Name>.Application/    # MediatR handlers, validators, DTOs. Depends on Domain.
├── PayFlow.<Name>.Infrastructure/ # EF Core, repository impls, external clients. Depends on Application + Domain.
└── PayFlow.<Name>.API/            # ASP.NET Core hosting. Depends on Infrastructure + Application + Domain.
```

The dependency direction is enforced by an architecture test (a `xUnit` test in the service's test project that uses `NetArchTest.Rules` to assert no upward references). Domain compiling against Application is a CI failure, not a discussion.

## File and namespace conventions

- One public type per file. Private/internal types may sit alongside.
- File name matches the public type.
- Namespace matches folder structure under the project root: `PayFlow.Transaction.Domain.Aggregates` for `Aggregates/` under the Domain project.
- File-scoped namespaces (`namespace X;`) rather than block scoped.

## Naming

Standard C# naming with these specifics:

| Construct | Convention | Example |
|---|---|---|
| Interfaces | `I` prefix | `IPaymentProvider` |
| Async methods | `Async` suffix when returning `Task`/`ValueTask`/`IAsyncEnumerable` | `ChargeAsync` |
| Domain events | Suffix `DomainEvent` | `TransactionCapturedDomainEvent` |
| Integration events | Suffix `IntegrationEvent` | `TransactionCapturedIntegrationEvent` |
| MediatR command | Suffix `Command` | `CreateTransactionCommand` |
| MediatR query | Suffix `Query` | `GetTransactionQuery` |
| MediatR handler | Suffix `Handler` | `CreateTransactionCommandHandler` |
| Result types | Suffix `Result` | `PaymentResult` |
| Database tables | snake_case | `transaction_state_history` |
| HTTP routes | kebab-case | `/api/webhook-deliveries` |
| JSON properties | snake_case | `transaction_id` |

JSON ↔ C# is bridged by `System.Text.Json` with a global naming policy. Don't override per-property unless you have a reason.

## File-level conventions

- `#nullable enable` is on for every project.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props`.
- `using` directives are sorted; the formatter enforces this.
- Type aliases (`using Foo = X.Y.Foo;`) are avoided unless the name conflict is unavoidable.

## Patterns we lean on

### Aggregates

A domain aggregate inherits from `AggregateRoot<TId>` (in `PayFlow.SharedKernel`) and exposes only methods (no public setters). State is private; state is changed through methods that:

1. Validate preconditions (state, invariants).
2. Mutate.
3. Raise a domain event.

```csharp
public void MoveToCaptured(string providerCode, string providerReference)
{
    EnsureState(TransactionState.Initiated, TransactionState.Authorized);
    State = TransactionState.Captured;
    FinalProviderCode = providerCode;
    CapturedAt = _clock.UtcNow;
    Raise(new TransactionCapturedDomainEvent(Id, TenantId, providerCode, providerReference));
}
```

If you find yourself adding a public setter on an entity, stop. Either the operation should be a method on the aggregate, or the field should not be settable.

### Value objects

Records for immutable values:

```csharp
public sealed record Money(long AmountMinor, string Currency)
{
    public static Money TRY(decimal amount) => new((long)(amount * 100), "TRY");
}
```

Validation goes in the constructor or a factory. `Money` rejects negative amounts and unknown currencies.

### Result type

Operations that can fail return `Result<T>` rather than throwing:

```csharp
public Result<Transaction> Initiate(...)
{
    if (...) return Result.Failure<Transaction>("ROUTING_NO_PROVIDER_CONFIGURED");
    ...
    return Result.Success(transaction);
}
```

Exceptions are reserved for *unexpected* failures (DB down, configuration missing, programmer error). Business-rule violations are results, not exceptions.

### MediatR

The Application layer's entry points are MediatR commands and queries. Pipeline behaviours (in `PayFlow.WebApi.Shared`) handle validation, transaction wrapping, logging, and metrics — handlers are pure business logic.

A handler that takes more than one external dependency (a repository plus a domain service is normal; a repository plus three other services is a smell) probably wants the logic factored into a domain service.

### Repositories

One repository per aggregate, defined as an interface in `Domain`, implemented in `Infrastructure`. The interface returns *materialised* results (`Task<Transaction?>`, `Task<IReadOnlyList<Transaction>>`), never `IQueryable`. This keeps the EF filter discipline (see [multi-tenancy-isolation.md](../database/multi-tenancy-isolation.md)) within the repository.

The exception is read-side queries in the Reporting service — those projections are explicitly outside the aggregate model and can use Dapper / EF projections directly.

## What we avoid

### `IQueryable` leaking out of repositories

It defeats the global filter. The compile-time signature stops at the repository.

### `async void`

Allowed only in event handlers (button click in the dashboard's React side, which is not C#). In backend C# code, never. The CI linter flags it.

### `ConfigureAwait(false)` everywhere

ASP.NET Core has no synchronisation context. The cargo-culted `ConfigureAwait(false)` adds noise. We don't use it.

### Static service locators

`ServiceLocator.GetService<X>()` is forbidden. DI is constructor injection.

### `[Obsolete]` as a substitute for deletion

If something is unused, delete it. `[Obsolete]` is for things we cannot delete yet because of compatibility.

### "Helper" / "Utility" classes

Anything called `XxxHelper` or `XxxUtility` is a missing abstraction. Rename to what it actually does. If you cannot, the code is doing too many things.

## Exception handling

The global exception handler in `PayFlow.WebApi.Shared` maps:

- `ValidationException` (FluentValidation) → 422 with field-by-field details.
- `ResourceNotFoundException` → 404.
- `TenantMismatchException` → 500 (this is a bug; should not happen).
- `IdempotencyException` (subtypes) → corresponding 4xx codes.
- Anything else → 500 with an opaque trace id.

Stack traces are never returned to clients. The trace id is.

## Logging in code

One thing worth saying about logging:

```csharp
// Do this:
_logger.LogInformation("Transaction {TransactionId} moved to {State}", txId, state);

// Not this:
_logger.LogInformation($"Transaction {txId} moved to {state}");
```

Structured logging is non-negotiable. The CI analyzer flags interpolated log messages.

## Comments

Not many. Reasons:

- Comments describing *what* code does should be deleted in favour of better names.
- Comments describing *why* a non-obvious choice was made are kept.
- A comment that contradicts the code is worse than no comment.

XML doc comments on public interfaces are encouraged when the contract is not obvious. We do not require them on every method.

## Async patterns

- `Task` returning when the caller might want to wait; `ValueTask` when most calls return synchronously.
- `CancellationToken` is the last parameter, named `ct`. Always passed; never ignored. The CI analyzer flags ignored tokens.
- Don't fire-and-forget. If a method returns `Task`, await it. If you genuinely want background work, use `BackgroundService` or `IHostedService` — not a `Task.Run` from inside a handler.

## DI lifetimes

- **Singleton:** stateless services with no per-request state (clock, ID generator, config).
- **Scoped:** anything tied to the request (DbContext, ITenantContext, handlers).
- **Transient:** rare. Use scoped unless you have a specific reason.

DbContext is scoped, not transient. Sharing it across a request is the point.

## Code review focus

Reviewers prioritise, in order:

1. **Correctness on the domain.** Does the aggregate's state machine allow this transition?
2. **Tenant safety.** Is everything filtered?
3. **Test coverage.** Is the new code reachable from a test that would catch a regression?
4. **Readability.** Can someone unfamiliar with this code understand it next quarter?

Style nits (curly brace placement, naming a variable `x` vs `xs`) get addressed by the formatter, not the reviewer.

## Migrations

EF Core migrations:

- One migration per logical change. `dotnet ef migrations add CreateOutboxTable` not `Migration0042`.
- Generated SQL is reviewed in PR. EF can produce surprising SQL.
- Index names are explicit: `IX_outbox_messages_state_next_attempt`.
- Down migrations are not required (we forward-only in prod); they are not deleted from the source though.

A migration that includes a *data* change as well as a schema change is documented in the migration class's XML comment with the reasoning. Reviewers pay extra attention to those.

## Adding a new service

If you find yourself adding an eighth service, this is the checklist:

1. New folder under `Services/`.
2. New schema in Postgres + new EF DbContext.
3. New entry in [bounded-contexts.md](../architecture/bounded-contexts.md) classifying the context.
4. Update [bounded-contexts.md](../architecture/bounded-contexts.md) with the new context's classification and what it owns and consumes.
5. New entry in [c4-container.md](../architecture/c4-container.md).
6. ADR if the service introduces new patterns (rare — most services follow the existing template).

The point of the checklist is that "I added a service" is not just a code change; it is a documentation update across several files. Reviewers reject PRs that ship a service without updating the docs.
