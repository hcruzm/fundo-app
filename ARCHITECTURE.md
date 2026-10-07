# Architecture

Setup, test commands and test data are in [`README.md`](README.md).

## 1. Structure and layer responsibilities

```
backend/src/
  Fundo.LoanApp.Domain/          Customer aggregate, Address, SsnHash, rule engine, ports. No packages.
  Fundo.LoanApp.Application/     SubmitLoanApplicationHandler, GetLoanApplicationHandler, IUnitOfWork,
                                 IEventPublisher, ISsnHasher
  Fundo.LoanApp.Infrastructure/  EF Core + PostgreSQL, repositories, outbox, HTTP client, HMAC hasher
  Fundo.LoanApp.Api/             Minimal API endpoints, FluentValidation, ProblemDetails, composition root
backend/tests/                   Unit tests (no I/O) and integration tests (Testcontainers PostgreSQL)
frontend/                        Next.js form, approved page, denied page
mock-service/                    The external service: one upsert endpoint, in-memory
```

Dependencies point inward: Api → Infrastructure → Application → Domain. The Domain declares
the repository interfaces it needs; Infrastructure implements them. `DependencyRuleTests`
(NetArchTest) fails if the Domain references EF Core, ASP.NET Core or any outer layer.

The endpoint is thin: it calls one handler and maps `Approved` to `201` and `Denied` to `200`.

**The aggregate.** `Customer` is the aggregate root and owns exactly one `LoanApplication`.
`Customer.Create(...)` opens both; `customer.Reapply(...)` updates both in place. The
application's factory and mutator are `internal`, so the one-customer-to-one-application rule
cannot be bypassed from outside the Domain, and a unique index on `applications.customer_id`
enforces it in the database too.

## 2. Rule engine

```csharp
public interface IDenialRule
{
    Task<RuleOutcome> EvaluateAsync(LoanApplicationCandidate candidate, CancellationToken ct);
}
```

`DecisionEngine` receives every registered `IDenialRule`, evaluates them in registration order,
and returns `Denied(reason)` on the first denial. If no rule objects, the application is
approved. Rules only see `LoanApplicationCandidate`, which carries the SSN hash, never the SSN.

| Rule | Denies when |
|---|---|
| `RestrictedStateRule` | the state is in `Decision:RestrictedStates` (`NY`) |
| `BlacklistedSsnRule` | the SSN hash is in `blacklisted_ssns` |

**Adding a rule:** create a class implementing `IDenialRule` in
`Domain/Decisions/Rules/`, then add `services.AddScoped<IDenialRule, MyRule>();` in
`Infrastructure/DependencyInjection.cs`. No existing rule, the engine or the handler changes.

The blacklist denial message is deliberately generic so it does not reveal that a list exists.

## 3. Background event and the external service

The event must be in the same transaction as the records and processed outside the request.
A transactional outbox does both:

```
POST /api/applications → SubmitLoanApplicationHandler
  BEGIN
    find customer by SSN hash → Customer.Create(...) or customer.Reapply(...)
    IEventPublisher.Publish(CustomerUpsertedEvent)  → INSERT outbox_messages
  COMMIT  (customer, application and event, or none of them)
  → 201 response

OutboxProcessor (BackgroundService, every 2 s)
  SELECT pending outbox rows, oldest first
  for each: re-read the customer → PUT /api/customers/{ssnHash}
            success → mark processed_at   failure → leave pending, retry next poll
```

- **Contract.** One endpoint, `PUT /api/customers/{ssnHash}`, with the customer and its
  application. It creates or replaces and always returns `200`. The outbox delivers *at least
  once*, so the receiver must be idempotent; an upsert is, and it does not depend on both sides
  agreeing whether the customer already exists. The SSN hash is the key because it is the
  customer's stable identity here and is not the SSN.
- **Payload.** The event carries only ids; the processor re-reads committed state, so a late
  or repeated delivery sends current data. Only the SSN hash and last four digits leave the system.
- **Retries.** Each call uses `AddStandardResilienceHandler()` (backoff, circuit breaker,
  timeouts) for transient errors. If that gives up, the row stays pending and is retried on the
  next poll, so an outage of any length recovers once the service is back.

## 4. Transactions and failure behaviour

`EfUnitOfWork.ExecuteInTransactionAsync` opens a transaction, runs the use case, calls
`SaveChangesAsync` once and commits. Repositories never save. Any exception disposes the
transaction uncommitted, which rolls it back.

| Failure | Result |
|---|---|
| Application denied | Nothing written, no event. `200 OK` with the reason. |
| Any database write fails | Rollback: no customer, no application, no outbox row. `500` ProblemDetails with a correlation id. |
| External service down | Applicant unaffected (already committed and answered). Row stays pending and is delivered when the service recovers. |
| API process dies after commit | The event is in the table; the processor delivers it after restart. |
| Database down at startup | The host fails to start rather than serve requests. |

`TransactionTests` proves the rollback against real PostgreSQL: rows written inside the
transaction are invisible to a second connection after a forced failure. `OutboxProcessorTests`
proves a delivered message is sent once and marked, and a failed one stays pending.

## 5. Trade-offs and omissions

| Decision | Why |
|---|---|
| Outbox polled by a `BackgroundService`, no broker | The requirement is transactional publishing. A broker cannot join a PostgreSQL transaction without an outbox anyway; one table and one poller is the smallest thing that meets it. |
| SSN stored as HMAC-SHA256 + last four | The system only needs to recognise an SSN, never read it back. A keyed digest keeps lookups indexed; an unkeyed hash of nine digits is brute-forceable. |
| `200 OK` for a denial | A denial is a valid answer to a well-formed request; `400` is for malformed input. |
| One customer, one application | The literal reading of "update instead of duplicate". Supporting history would need product decisions the challenge does not make. |
| Migrations and seed run at startup; dev SSN key committed | The project runs right after a clone. Production would migrate in a deploy step and take the key from a secret store. |
| zod schema mirrors FluentValidation | Instant feedback in the form; the server stays the authority. |

**Left out:** authentication (not required; `GET /api/applications/{id}` would need an owner
check in production), rate limiting, storing denied applications, multi-instance outbox claiming
(`FOR UPDATE SKIP LOCKED`), a dead-letter state for permanently failing messages, cleanup of
processed outbox rows, and a `409` instead of a `500` when two requests with the same new SSN race
on the unique index.
