# Architecture

This document explains how the system is put together, why it is put together that way, and
what was deliberately left out. Setup and run instructions are in [`README.md`](README.md).

---

## 1. Project structure and layer responsibilities

```
fundo-loan-application/
├── docker-compose.yml              PostgreSQL 17 + the mock external service
├── backend/
│   ├── Directory.Build.props       net10.0, nullable, TreatWarningsAsErrors
│   ├── Fundo.LoanApp.sln
│   ├── src/
│   │   ├── Fundo.LoanApp.Domain/           entities, value objects, rule engine, ports
│   │   ├── Fundo.LoanApp.Application/      use cases and the ports they need
│   │   ├── Fundo.LoanApp.Infrastructure/   EF Core, outbox, HTTP client, hashing
│   │   └── Fundo.LoanApp.Api/              minimal API, validation, composition root
│   └── tests/
│       ├── Fundo.LoanApp.UnitTests/        42 tests, no I/O
│       └── Fundo.LoanApp.IntegrationTests/ 26 tests, Testcontainers PostgreSQL
├── frontend/                       Next.js App Router, shadcn/ui, react-hook-form + zod
└── mock-service/                   single-file minimal API standing in for a third party
```

| Project | Depends on | Holds |
|---|---|---|
| `Fundo.LoanApp.Domain` | nothing — the `.csproj` has no `PackageReference` at all | `Customer`, `LoanApplication`, `Address`, `SsnHash`, `Decision`, `RuleOutcome`, `DecisionEngine`, `IDenialRule` and the two rules, `CustomerUpsertedEvent`, and the three repository interfaces |
| `Fundo.LoanApp.Application` | Domain | `SubmitLoanApplicationHandler`, `GetLoanApplicationHandler`, `ListLoanApplicationsHandler`, their commands and DTOs, and the ports `IUnitOfWork`, `IEventPublisher`, `ISsnHasher` |
| `Fundo.LoanApp.Infrastructure` | Application (and Domain through it) | `LoanAppDbContext`, entity configurations, migrations, the three repositories, `EfUnitOfWork`, `HmacSsnHasher`, `OutboxEventPublisher`, `OutboxProcessor`, `CustomerUpsertedDispatcher`, `ExternalServiceClient`, `DatabaseInitializer`, `BlacklistSeeder`, and the `AddInfrastructure` registration extension |
| `Fundo.LoanApp.Api` | Infrastructure | endpoint group, request contracts, FluentValidation validators and the validation filter, `GlobalExceptionHandler`, OpenAPI + Scalar, CORS |

Two things follow from that table.

**The repository interfaces live in Domain, the implementations in Infrastructure.** The
Domain declares what it needs; the outer layer supplies it. That is the whole of the
dependency inversion in this project, and it is why `SubmitLoanApplicationHandler` is unit
tested against a handful of hand-written stubs with no database and no mocking library.

**The dependency direction is a test, not a comment.**
`backend/tests/Fundo.LoanApp.UnitTests/Architecture/DependencyRuleTests.cs` uses NetArchTest
to assert that no type in `Fundo.LoanApp.Domain` depends on `Application`, `Infrastructure`,
`Api`, `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`, `Microsoft.Extensions`, or
`Npgsql`. An EF Core attribute or an `IOptions<T>` that leaks into the Domain fails a test;
nobody has to notice it in review.

One consequence of the "no `Microsoft.Extensions` in Domain" rule is visible in
`RestrictedStates`: it is a plain Domain record wrapping a set of state codes, bound from
`Decision:RestrictedStates` and registered as an instance in `AddInfrastructure`, rather than
an `IOptions<T>` injected into the rule. The rule stays a Domain type with a Domain
dependency.

The endpoint layer contains no business branching. `SubmitAsync` calls one handler and maps
`Approved` to `201 Created` and `Denied` to `200 OK`. Every decision it maps was made in the
rule engine.

### Where a request goes

```
POST /api/applications
  │
  ├─ ValidationFilter<SubmitApplicationRequest>  ──► 400 ValidationProblemDetails
  │
  └─ SubmitLoanApplicationHandler
       1. ssnHash = ssnHasher.Hash(command.Ssn)
       2. decision = decisionEngine.DecideAsync(candidate)
       3. Denied  ──► return Denied(reason)        nothing written, no event
       4. Approved ─► unitOfWork.ExecuteInTransactionAsync(...)
                        new SSN      → Customer.Create + LoanApplication.Create
                        existing SSN → UpdateDetails + UpdateRequestedAmount
                        eventPublisher.Publish(CustomerUpsertedEvent)  → outbox row
                      COMMIT   (customer, application and event, or none of them)
       5. return Approved(applicationId, customerId, isReturningCustomer)
```

---

## 2. The rule engine

The contract is one method:

```csharp
public interface IDenialRule
{
    Task<RuleOutcome> EvaluateAsync(LoanApplicationCandidate candidate, CancellationToken ct);
}
```

`DecisionEngine` takes `IEnumerable<IDenialRule>`, walks it in order, and returns
`Decision.Denied(reason)` on the first `RuleOutcome` that is a denial. If every rule passes,
the decision is approved. There is no scoring, no aggregation of reasons, and no rule that can
approve — a rule can only object. That asymmetry is what keeps the engine a `foreach` instead
of a policy framework.

`LoanApplicationCandidate` is the only thing a rule sees, and it carries `SsnHash`, never the
plaintext SSN. A new rule cannot accidentally get hold of the raw value.

The two rules:

| Rule | Denies when | Dependency |
|---|---|---|
| `RestrictedStateRule` | `candidate.Address.State` is in the configured set (`NY`) | `RestrictedStates`, in memory |
| `BlacklistedSsnRule` | `candidate.SsnHash` is present in `blacklisted_ssns` | `IBlacklistedSsnRepository`, one indexed query |

`EvaluateAsync` is asynchronous even for the state check, which returns
`Task.FromResult(...)`. The alternative — a synchronous contract with an async escape hatch —
would have forced the second rule to block on a database call.

**Registration order is evaluation order.** In
`Fundo.LoanApp.Infrastructure/DependencyInjection.cs`:

```csharp
// Registration order is evaluation order: the in-memory check runs before the query.
services.AddScoped<IDenialRule, RestrictedStateRule>();
services.AddScoped<IDenialRule, BlacklistedSsnRule>();
```

The in-memory rule runs first, so an application from NY is denied without touching the
database. This is a real ordering dependency, and it is the one thing about the engine that is
implicit rather than declared — an explicit `Order` property would make it visible, at the cost
of another concept for two rules. The comment carries it for now.

**Adding a rule is two steps:**

1. Add a class implementing `IDenialRule` in `backend/src/Fundo.LoanApp.Domain/Decisions/Rules/`.
2. Add one line: `services.AddScoped<IDenialRule, MyNewRule>();` — placed where you want it
   evaluated.

`DecisionEngine`, `SubmitLoanApplicationHandler`, and the endpoints are untouched. If the rule
needs data, declare a repository interface in the Domain and implement it in Infrastructure;
the rule itself stays a Domain type.

**Denial messages are part of the design.** `RestrictedStateRule` names the state, because the
applicant can act on that. `BlacklistedSsnRule` returns a deliberately generic
`"This application cannot be processed."` — a specific message would tell the applicant they
are on a list, which discloses the list. The comment in the rule says so, so that nobody
"improves" the message later.

---

## 3. Background event workflow

The challenge asks for two things that pull against each other: the event must be part of the
same transaction as the records, and it must be processed outside the HTTP request. A
transactional outbox gives both.

```
SubmitLoanApplicationHandler          (inside ExecuteInTransactionAsync)
   └─ IEventPublisher.Publish(CustomerUpsertedEvent(customerId, applicationId))
        └─ OutboxEventPublisher → adds an outbox_messages row to the change tracker
   COMMIT  ── customer + application + outbox row, atomically

OutboxProcessor : BackgroundService   (every 2 s, outside any request)
   └─ SELECT pending rows (processed_at IS NULL) ORDER BY created_at LIMIT 20
        └─ for each: CustomerUpsertedDispatcher
             ├─ re-reads the Customer and the LoanApplication from the database
             └─ ExternalServiceClient → PUT /api/customers/{ssnHash}   (upsert)
           success → processed_at = now, SAVE
           failure → log, leave pending, retried on the next poll
```

**The event is written in the transaction, not after it.** `IEventPublisher` did not change;
its implementation did. `OutboxEventPublisher` only adds a row to the same `DbContext`, so the
`SaveChangesAsync` + `COMMIT` in `EfUnitOfWork` persists the customer, the application and the
event together. A rollback discards all three. There is no window in which the records exist
and the event does not, or the other way round.

**The request never waits on the third party.** The handler's only extra work is one more
`INSERT` in a transaction it already runs. Delivery happens in `OutboxProcessor`, which
resolves its own DI scope per poll because the dispatcher depends on a scoped `DbContext`.

**The dispatcher re-reads from the database instead of carrying data on the event.** The event
carries two ids. Whatever is sent is committed state, so a delayed or repeated delivery sends
current data rather than a stale snapshot.

### The external service contract

One endpoint: `PUT /api/customers/{ssnHash}` with the customer and its application in the
body. It creates the record if the hash is unknown and replaces it otherwise, and always
answers `200 OK`.

- **Why one idempotent upsert instead of `POST` + `PUT`.** The outbox delivers at least once: a
  crash after the external call but before `processed_at` is saved sends the same event again.
  An upsert makes that harmless. It also removes any need for this system to know whether the
  external service already has the customer — a create/update split decided from *our*
  database breaks as soon as the two sides disagree.
- **Why the SSN hash is the key.** It is already the customer's identity here, it is stable
  across submissions, and it is not the SSN.

**Retries.** Two layers. Each call goes through `.AddStandardResilienceHandler()` (retry with
exponential backoff and jitter, circuit breaker, timeouts) for transient blips. If that gives
up, the row stays pending and the next poll tries again, so an outage of any length is
recovered once the service is back. Messages are processed oldest first; because each
delivery sends current state, order across retries does not affect the final result.

**Left out on purpose.** No attempt counter or dead-letter state — a permanently failing
message is retried every poll and logged each time. No multi-instance claiming
(`FOR UPDATE SKIP LOCKED`): there is one API instance; with more, two processors could send the
same message twice, which the idempotent upsert already tolerates. No cleanup of processed
rows.

---

## 4. Transaction handling and failure scenarios

`IUnitOfWork` owns the boundary, and it is the only place a commit happens:

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(ct);
var result = await operation(ct);
await db.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
return result;
```

There is no explicit catch/rollback: `await using` rolls the transaction back automatically
when it is disposed on the way out of a failed operation, and an explicit catch would risk
swallowing the original exception with an `OperationCanceledException` if the request was
already aborted.

Repositories only `Add` to the change tracker and query; none of them calls
`SaveChangesAsync`. The use case decides what belongs in one atomic unit, which is exactly
where that decision belongs — a repository cannot know whether it is the last write in a
sequence.

`ExecuteInTransactionAsync` returns a value, so the handler gets the ids it needs out of the
transaction without reaching into the `DbContext` afterwards.

| Failure | Behaviour |
|---|---|
| The application is denied | Nothing is written, no event is published, the response is `200 OK` with the reason. |
| The second write fails — the customer is inserted, the application insert then hits a constraint violation or a dropped connection | Both rows go through one `SaveChangesAsync` inside the transaction, so the exception propagates out of `ExecuteInTransactionAsync`, `await using` rolls the transaction back on disposal, and **neither** row persists — nor the outbox row, which was part of the same save. No event exists. `GlobalExceptionHandler` returns `500` `ProblemDetails` with a `correlationId` and a generic title; the exception detail goes to the logs only. |
| A returning customer has no application row | `InvalidOperationException` inside the transaction — same path as above. The unique index and the one-to-one assumption make this a broken invariant, not a normal case. |
| The external service is down or slow | The applicant is unaffected: the transaction has already committed and the response has already been sent. The resilience pipeline retries; if it gives up, the processor logs the failure and the outbox row stays pending. It is delivered on a later poll once the service is back. |
| The API process dies after the commit | The event is in `outbox_messages`. The processor picks it up when the API starts again. |
| The database is unreachable at startup | `DatabaseInitializer` throws from `StartAsync` and the host fails to start, loudly, rather than serving requests against a database that is not there. |

`TransactionTests` in the integration suite proves this against a real PostgreSQL container:
the customer row is written and asserted visible *inside* the transaction, then a failure is
forced, and a second connection sees both tables and the outbox empty. `OutboxProcessorTests`
proves a delivered message is sent once and marked processed, and a failed one stays pending.

---

## 5. Data model and SSN handling

Four tables, created by EF Core migrations that run at startup:

```sql
customers (
  id uuid PK, ssn_hash text, ssn_last4 char(4),
  first_name varchar(100), last_name varchar(100), company_name varchar(200),
  street varchar(200), city varchar(100), state char(2), postal_code varchar(10),
  created_at timestamptz, updated_at timestamptz )
CREATE UNIQUE INDEX ix_customers_ssn_hash ON customers (ssn_hash);

applications (
  id uuid PK, customer_id uuid NOT NULL REFERENCES customers (id) ON DELETE RESTRICT,
  requested_amount numeric(18,2), status varchar(20),
  created_at timestamptz, updated_at timestamptz )
CREATE UNIQUE INDEX ix_applications_customer_id ON applications (customer_id);

blacklisted_ssns ( ssn_hash text PK, note varchar(200) )

outbox_messages (
  id uuid PK, payload jsonb, created_at timestamptz, processed_at timestamptz NULL )
CREATE INDEX ix_outbox_messages_pending ON outbox_messages (created_at) WHERE processed_at IS NULL;
```

The address is an EF Core owned type: an `Address` value object in the Domain, four columns on
`customers` in the database. It has no identity of its own, so it gets no table of its own.

Ids are `Guid.CreateVersion7()` — UUIDv7 is time-ordered, so inserts stay at the right-hand
edge of the B-tree instead of scattering across it the way UUIDv4 does.

**`ix_customers_ssn_hash` is unique** because the hashed SSN is the identity of a customer in
this system. It is what makes "have I seen this applicant before" a single indexed equality
lookup, and it makes a duplicate customer impossible even under a race between two concurrent
submissions of the same SSN — one of them fails at the constraint and rolls back rather than
creating a second customer. The losing client sees an unhandled `DbUpdateException` surfaced by
`GlobalExceptionHandler` as a `500`, not a friendlier conflict response.

**`ix_applications_customer_id` is unique** because this system deliberately holds exactly one
application per customer. The challenge says a returning customer's records should be
*updated* rather than duplicated, and this is the literal reading of that: one customer, one
application, updated in place. It is an assumption, not a fact about lending, and it is stated
here because it is the assumption a reviewer is most likely to want to challenge. Section 6
says what changes if it is wrong.

`status` is a string column, not an integer, so a database dump is readable without a lookup
table. In practice it always contains `Approved`: denials are not persisted at all, which is
why `ApplicationStatus` currently has a single member. Storing denials would mean a `Denied`
member, a `denial_reason` column, and a decision about whether a returning applicant who is
now denied should overwrite an earlier approval — a set of product questions the challenge
does not ask.

### SSN handling

The full SSN is never stored. `HmacSsnHasher` strips non-digits, rejects anything that is not
exactly nine digits, and computes `HMAC-SHA256(digits, key)`, storing the lowercase hex digest
in `ssn_hash`. The key comes from `Security:SsnHashKey` and must be at least 32 bytes; the
constructor throws if it is shorter. The last four digits are stored separately in
`ssn_last4`, and `GET /api/applications/{id}` returns `•••-••-6789` — the full value is not
in any response.

Why an HMAC rather than a plaintext column, and why an HMAC rather than a per-row salted hash:

- Plaintext means the SSN is in every backup, every replica, and every `SELECT *` a support
  engineer runs. The system never needs to read the SSN back, only to recognise it, so storing
  it would be storing a liability for no capability.
- A per-row salted password hash (bcrypt, Argon2) is the right tool when you verify one
  candidate against one stored value. Here the lookup is "find the customer with this SSN",
  which needs a *deterministic* digest to be an indexed equality query. A per-row salt would
  turn it into a table scan with a KDF per row.
- The keyed HMAC is what a plain SHA-256 is not: the SSN space is nine digits — a billion
  values, trivially enumerable. An unkeyed digest is reversible by brute force in minutes. The
  key is what makes the stored digest useless to someone who has the database but not the
  application configuration.

The honest limit of this: a real deployment handling real SSNs would put the value in a
dedicated vault or tokenization service, so the application never holds the plaintext at all,
not even in memory during a request, and the key rotation problem becomes the vault's. That is
infrastructure this challenge does not have, and imitating it badly would be worse than doing
this well.

---

## 6. Trade-offs and omissions

### Decisions taken

| Decision | Alternative | Why this one |
|---|---|---|
| Transactional outbox polled by a `BackgroundService` | In-process `Channel<T>`, or a message broker | The challenge requires the event to be in the same transaction as the records. A channel is written after the commit and lost if the process dies; a broker cannot join a PostgreSQL transaction without an outbox anyway. One table and one poller is the smallest thing that meets the requirement. |
| One idempotent `PUT` upsert to the external service | Separate `POST` create and `PUT` update | At-least-once delivery needs an idempotent receiver, and an upsert does not depend on both sides agreeing on whether the customer already exists. See section 3. |
| One customer to one application, enforced by a unique index | One customer to many applications | The challenge says update the returning customer's records instead of duplicating them. The unique index makes the assumption explicit and enforced instead of implied by handler code. |
| HMAC-hashed SSN + last four | Plaintext SSN column | Keeps the SSN out of the database while preserving a single indexed lookup. See section 5. |
| `200 OK` for a denial | `422 Unprocessable Entity` | A denial is a valid business answer to a well-formed request. The client did nothing wrong, so it is not a 4xx. `400` is reserved for a malformed body. |
| `decimal` with `numeric(18,2)` | A `Money` value object | The design called for `Money`. It was dropped: there is one currency, one amount, and no arithmetic anywhere in the system. A wrapper that only wraps is ceremony, and `numeric(18,2)` already prevents the float rounding that `Money` usually exists to prevent. It becomes worth building the moment a second currency or any arithmetic appears. |
| zod schema duplicated from FluentValidation | Generated client types from OpenAPI | Nine fields. Codegen means a build step, a generated-code checkout policy, and a stale-artifact failure mode. The backend remains the authority — the client copy exists only for immediate feedback, and the server re-validates everything. This is a real duplication and it is the first thing that would rot if the form grew. |
| Testcontainers PostgreSQL | Shared local database, or the EF in-memory provider | Real transactions, real constraints, no shared state between test classes, and no setup step for the reviewer beyond Docker. The in-memory provider cannot prove a rollback, which is the behaviour most worth testing here. |
| Migrations and the seed run at startup | A separate migration step | One fewer command for a reviewer, and no "did you remember to migrate" failure mode. See "would need to change" below. |
| Committed development `SsnHashKey` | user-secrets or a required environment variable | The project runs immediately after a clone. It is a development value, and `README.md` states how a deployment supplies its own. |

### Left out on purpose

| Not built | Why |
|---|---|
| **Authentication and authorization** | The challenge states it is not required, and a public loan form is genuinely anonymous at the point of submission. The endpoint that would need protection is `GET /api/applications/{id}`: anyone holding the id can read the record, including the address and the masked SSN. UUIDv7 makes the ids impractical to enumerate, which is not the same thing as access control. The real work is an identity provider plus an owner check on the read endpoint. |
| **Rate limiting** | Without authentication or a public deployment there is nothing to protect and no threat model to size a limit against. It is one `builder.Services.AddRateLimiter(...)` and one `.RequireRateLimiting(...)` when there is. |
| **A real message broker** | RabbitMQ or Kafka would add a container, a connection lifecycle, consumer configuration and a serialization contract to move one event type between two processes on the same machine. The `IEventPublisher` port is the seam: swapping the implementation does not touch the handler. |
| **Pagination on `GET /api/applications`** | It returns every stored application in one response. That is fine for a demo with a handful of rows and would not be fine once the table grows; a real deployment needs `?page=` or keyset pagination before this is exposed at scale. |
| **Multi-application history per customer** | See the one-to-one assumption in section 5. Changing it means dropping the unique index, adding a "current application" concept or an explicit status per application, and deciding what a returning applicant's second submission means — a new application or an amendment to the open one. That is a product question, not a schema question. |
| **Internationalization** | One locale, US states, USD. |
| **Storing denied applications** | See section 5. There is no audit or reporting requirement here; adding the rows without the product decisions behind them would be guessing. |

`GET /api/applications` returns every applicant's data with no authentication. It exists so the
demo can show stored state, and because the challenge does not require authentication. In
production it would need authentication and role-based authorization; it is the one endpoint in
this system that would be unacceptable to expose as it stands.

### Would need to change before a real deployment

- **`DatabaseInitializer` migrates and seeds in every environment.** That is right for a
  reviewer running this locally and wrong for production, where migrations belong in a
  deployment step that can be gated, reviewed, and rolled back — and where a startup race
  between multiple instances all calling `MigrateAsync` is a real hazard. It needs an
  environment guard at minimum.
- **`Security__SsnHashKey` must come from the environment or a secret manager**, never from a
  committed file. Rotating it invalidates every stored hash, so a real system needs a rotation
  plan (dual-write both digests through a transition, or re-derive from a vault token).
- **The blacklist seed is demonstration data.** `111-11-1111` and `222-22-2222` are seeded so
  the denial path can be shown; a real blacklist has a source of record and an update process.
- **CORS currently allows `http://localhost:3000`** from configuration. A deployment sets its
  own origins.
- **Logging is the default console provider.** Production wants structured logs shipped
  somewhere, and the `correlationId` already attached to `500` responses wants to be a real
  trace id from OpenTelemetry rather than `HttpContext.TraceIdentifier`.
