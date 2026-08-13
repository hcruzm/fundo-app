# Loan Application System — Design

**Date:** 2026-08-13
**Status:** Approved, ready for implementation planning

---

## 1. Purpose

Build a loan application system with three deployable pieces:

1. A Next.js form that collects applicant data.
2. A .NET backend that decides the application through a rule engine, persists the result transactionally, and publishes an event.
3. A mock external service that receives customer and application data over HTTP.

The system must handle both new applicants and returning customers without creating duplicate records.

## 2. Requirements

### 2.1 Functional

| # | Requirement |
|---|---|
| F1 | Collect first name, last name, address (street, city, state, postal code), company name, requested amount, SSN. |
| F2 | Decide the application through a rule engine on the backend. No decision logic in controllers or components. |
| F3 | Deny when the applicant's state is `NY`. |
| F4 | Deny when the SSN is blacklisted. |
| F5 | On approval, create a Customer record and an Application record in a single database transaction. Partial failure rolls back both. |
| F6 | When the SSN already exists, update the existing Customer and Application instead of inserting duplicates. |
| F7 | After the transaction commits, publish an event that is processed outside the HTTP request that answers the form. |
| F8 | The background processor sends customer and application data to the external service. New customers produce a create; returning customers produce an update. |
| F9 | Denied applicants see a denial page stating the reason. |

### 2.2 Non-functional

| # | Requirement |
|---|---|
| N1 | Dependencies point inward: Domain depends on nothing; Api depends on everything and composes the graph. |
| N2 | A new denial rule requires one new class and one DI registration — no changes to the engine or the use case. |
| N3 | The full SSN is never stored. |
| N4 | Real database transactions. EF Core in-memory provider is not used, including in tests. |
| N5 | Local setup runs with documented commands and no manual database steps. |

### 2.3 Explicitly out of scope

Authentication, authorization, rate limiting, a real message broker, pagination, internationalization, and multi-application history per customer. Each is listed with its rationale in `ARCHITECTURE.md`.

## 3. Architecture

### 3.1 Repository layout

```
fundo-loan-application/
├── README.md
├── ARCHITECTURE.md
├── docker-compose.yml
├── backend/
│   ├── Fundo.LoanApp.sln
│   ├── Directory.Build.props
│   ├── src/
│   │   ├── Fundo.LoanApp.Domain/
│   │   ├── Fundo.LoanApp.Application/
│   │   ├── Fundo.LoanApp.Infrastructure/
│   │   └── Fundo.LoanApp.Api/
│   └── tests/
│       ├── Fundo.LoanApp.UnitTests/
│       └── Fundo.LoanApp.IntegrationTests/
├── frontend/
└── mock-service/
```

### 3.2 Layer responsibilities

**`Fundo.LoanApp.Domain`** — no external package references.

- Entities: `Customer`, `LoanApplication`.
- Value objects: `Address`, `Money`, `SsnHash`, `Decision`, `RuleOutcome`.
- Rule engine: `IDenialRule`, `DecisionEngine`, and the rule implementations.
- Repository interfaces: `ICustomerRepository`, `ILoanApplicationRepository`, `IBlacklistedSsnRepository`.
- Domain event: `CustomerUpsertedEvent`.

**`Fundo.LoanApp.Application`** — depends on Domain only.

- Use case: `SubmitLoanApplicationHandler`, `GetLoanApplicationHandler`.
- Request/response DTOs.
- Ports: `IUnitOfWork`, `IEventPublisher`, `ISsnHasher`.

**`Fundo.LoanApp.Infrastructure`** — depends on Application and Domain.

- `LoanAppDbContext`, EF Core entity configurations, migrations.
- Repository implementations backed by PostgreSQL.
- `HmacSsnHasher`.
- `ChannelEventPublisher` and `ExternalServiceWorker`.
- `ExternalServiceClient` (typed `HttpClient`).

**`Fundo.LoanApp.Api`** — composition root.

- Minimal API endpoint group, one file per resource.
- FluentValidation validators and a validation filter.
- `GlobalExceptionHandler` producing RFC 9457 `ProblemDetails`.
- OpenAPI document plus Scalar reference UI.
- CORS policy for the frontend origin.

Endpoints receive a request, validate its shape, call one handler, and map the result to an HTTP response. They contain no branching on business state beyond that mapping.

### 3.3 Dependency rule enforcement

`Fundo.LoanApp.UnitTests` contains a NetArchTest suite asserting that `Fundo.LoanApp.Domain` types have no dependency on `Application`, `Infrastructure`, `Api`, `Microsoft.EntityFrameworkCore`, or `Microsoft.AspNetCore`.

## 4. Rule engine

### 4.1 Contract

```csharp
public sealed record LoanApplicationCandidate(
    string FirstName,
    string LastName,
    Address Address,
    string CompanyName,
    decimal RequestedAmount,
    SsnHash SsnHash);

public sealed record RuleOutcome(bool IsDenial, string? Reason)
{
    public static RuleOutcome Pass() => new(false, null);
    public static RuleOutcome Deny(string reason) => new(true, reason);
}

public interface IDenialRule
{
    Task<RuleOutcome> EvaluateAsync(LoanApplicationCandidate candidate, CancellationToken ct);
}
```

### 4.2 Engine

```csharp
public sealed class DecisionEngine(IEnumerable<IDenialRule> rules)
{
    public async Task<Decision> DecideAsync(LoanApplicationCandidate candidate, CancellationToken ct)
    {
        foreach (var rule in rules)
        {
            var outcome = await rule.EvaluateAsync(candidate, ct);
            if (outcome.IsDenial)
            {
                return Decision.Denied(outcome.Reason!);
            }
        }

        return Decision.Approved();
    }
}
```

The engine short-circuits on the first denial. Evaluation order follows DI registration order, which places the cheap in-memory check before the database-backed one.

### 4.3 Rules

| Rule | Denies when | Dependencies |
|---|---|---|
| `RestrictedStateRule` | `candidate.Address.State` is in the restricted set (`NY`) | A `RestrictedStates` record injected through the constructor |
| `BlacklistedSsnRule` | `candidate.SsnHash` is present in `blacklisted_ssns` | `IBlacklistedSsnRepository` |

The rules are `async` so a rule may consult a repository or an external source without changing the contract. Rules that need no I/O return a completed task.

`RestrictedStates` is a Domain record holding the denied state codes. `Fundo.LoanApp.Api` binds it from configuration (`Decision:RestrictedStates`) and registers the instance, which keeps `Microsoft.Extensions.Options` out of the Domain project and satisfies the dependency test in section 3.3.

### 4.4 Adding a rule

1. Add a class implementing `IDenialRule` in `Domain/Decisions/Rules/`.
2. Register it with `services.AddScoped<IDenialRule, MyRule>()` in `DecisionServiceCollectionExtensions`.

No other file changes. Documented with this exact procedure in `ARCHITECTURE.md`.

## 5. Submission flow

```
POST /api/applications
  │
  ├─ FluentValidation: shape, ranges, required fields  ──► 400 ValidationProblemDetails
  │
  └─ SubmitLoanApplicationHandler
       1. ssnHash = hasher.Hash(request.Ssn)
       2. decision = await engine.DecideAsync(candidate, ct)
       3. decision is Denied
            └─► return DeniedResult(reason)          // nothing persisted, no event
       4. decision is Approved
            BEGIN TRANSACTION
              existing = await customers.FindBySsnHashAsync(ssnHash, ct)
              existing is null
                ├─ true  → customer = Customer.Create(...); customers.Add(customer)
                │          application = LoanApplication.Create(customer.Id, amount)
                │          applications.Add(application)
                └─ false → existing.UpdateDetails(...)
                           application = await applications.GetByCustomerIdAsync(existing.Id, ct)
                           application.UpdateRequestedAmount(amount)
              await unitOfWork.SaveChangesAsync(ct)
            COMMIT
       5. publisher.Publish(new CustomerUpsertedEvent(customerId, applicationId, isUpdate))
       6. return ApprovedResult(applicationId, isReturningCustomer)
```

Step 5 runs only after the transaction commits. A rollback in step 4 means no event, which keeps the external service consistent with the database.

## 6. HTTP contract

### 6.1 `POST /api/applications`

Request:

```json
{
  "firstName": "Ada",
  "lastName": "Lovelace",
  "companyName": "Analytical Engines LLC",
  "requestedAmount": 25000.00,
  "ssn": "123-45-6789",
  "address": {
    "street": "1 Byron Street",
    "city": "Austin",
    "state": "TX",
    "postalCode": "78701"
  }
}
```

Approved — `201 Created`, `Location: /api/applications/{id}`:

```json
{
  "decision": "Approved",
  "applicationId": "0198f2c1-...",
  "customerId": "0198f2c0-...",
  "isReturningCustomer": false
}
```

Denied — `200 OK`:

```json
{
  "decision": "Denied",
  "reason": "Applications from NY are not accepted."
}
```

A denial is a valid business outcome of a well-formed request, not a client error, so it is not a 4xx. This choice is recorded in `ARCHITECTURE.md`.

### 6.2 `GET /api/applications/{id}`

`200 OK` with the application, its customer, and the masked SSN (`•••-••-6789`). `404 Not Found` otherwise. This endpoint makes the `Location` header meaningful and backs the confirmation page.

### 6.3 Errors

All non-success responses are RFC 9457 `ProblemDetails`, produced by `AddProblemDetails()` and a global `IExceptionHandler`. Unhandled exceptions return `500` with a correlation id and no internal detail.

## 7. Persistence

### 7.1 Schema

```sql
CREATE TABLE customers (
    id           uuid PRIMARY KEY,
    ssn_hash     text        NOT NULL,
    ssn_last4    char(4)     NOT NULL,
    first_name   varchar(100) NOT NULL,
    last_name    varchar(100) NOT NULL,
    company_name varchar(200) NOT NULL,
    street       varchar(200) NOT NULL,
    city         varchar(100) NOT NULL,
    state        char(2)      NOT NULL,
    postal_code  varchar(10)  NOT NULL,
    created_at   timestamptz  NOT NULL,
    updated_at   timestamptz  NOT NULL
);
CREATE UNIQUE INDEX ix_customers_ssn_hash ON customers (ssn_hash);

CREATE TABLE applications (
    id               uuid PRIMARY KEY,
    customer_id      uuid          NOT NULL REFERENCES customers (id),
    requested_amount numeric(18,2) NOT NULL,
    status           varchar(20)   NOT NULL,
    created_at       timestamptz   NOT NULL,
    updated_at       timestamptz   NOT NULL
);
CREATE UNIQUE INDEX ix_applications_customer_id ON applications (customer_id);

CREATE TABLE blacklisted_ssns (
    ssn_hash text PRIMARY KEY,
    note     text NOT NULL
);
```

`ix_applications_customer_id` is unique, which enforces the one-customer-to-one-application assumption at the database level.

### 7.2 SSN handling

The full SSN never reaches the database. `HmacSsnHasher` computes `HMAC-SHA256(normalizedSsn, key)` where `normalizedSsn` strips non-digits and the key comes from configuration (`Security:SsnHashKey`, supplied through user-secrets locally and an environment variable in `docker-compose.yml`). The hash is the lookup key for returning customers and for the blacklist. `ssn_last4` is stored separately so the UI can display a masked value.

The hash is deterministic, which is what makes the returning-customer lookup a single indexed equality query. `ARCHITECTURE.md` records that a production system would additionally tokenize through a dedicated vault, and why that was out of scope here.

### 7.3 Transaction boundary

`IUnitOfWork.ExecuteInTransactionAsync(Func<CancellationToken, Task<T>>, CancellationToken)` wraps the work in `dbContext.Database.BeginTransactionAsync()`, commits on success, and rolls back on any exception. The handler owns the boundary; repositories never commit.

### 7.4 Seed data

An `IHostedService` runs `Database.MigrateAsync()` on startup and seeds `blacklisted_ssns` with hashes of the documented test SSNs. Seeding is idempotent.

## 8. Background event processing

### 8.1 Publisher

```csharp
public sealed class ChannelEventPublisher(Channel<CustomerUpsertedEvent> channel) : IEventPublisher
{
    public void Publish(CustomerUpsertedEvent evt) => channel.Writer.TryWrite(evt);
}
```

The channel is an unbounded singleton `Channel<CustomerUpsertedEvent>`. `Publish` does not await, so the HTTP request that answers the form returns without waiting on the external service.

### 8.2 Worker

`ExternalServiceWorker : BackgroundService` reads with `await foreach (var evt in channel.Reader.ReadAllAsync(ct))`, resolves a scoped `ExternalServiceClient`, loads the current customer and application, and calls:

- `POST {ExternalService:BaseUrl}/api/customers` when `IsUpdate` is false.
- `PUT {ExternalService:BaseUrl}/api/customers/{ssnHash}` when `IsUpdate` is true.

Transient failures retry with exponential backoff through `Microsoft.Extensions.Http.Resilience`. Exhausted retries log an error and drop the event; the worker keeps draining the channel.

### 8.3 Known limitation

The queue is in-process. A crash between commit and delivery loses the event. `ARCHITECTURE.md` states this and names the transactional outbox as the next step, with the reason it was not built: it adds a table, a poller, and idempotency handling for a guarantee the challenge does not require.

## 9. Mock external service

A single-file .NET minimal API on port 5100:

| Method | Route | Behavior |
|---|---|---|
| `POST` | `/api/customers` | Stores the payload keyed by `ssnHash`. `409` if the key exists. |
| `PUT` | `/api/customers/{ssnHash}` | Replaces the payload. `404` if the key is absent. |
| `GET` | `/api/customers` | Lists everything received, so the demo video can show the data arriving. |

State lives in a `ConcurrentDictionary`. Every request is logged to stdout.

## 10. Frontend

Next.js 15 App Router with shadcn/ui.

| Route | Content |
|---|---|
| `/` | The application form. |
| `/applications/[id]` | Approval confirmation: application id, requested amount, masked SSN, and whether the record was updated. |
| `/denied` | Denial reason and a link back to the form. |

`react-hook-form` with a `zod` resolver handles client validation. The zod schema mirrors the FluentValidation rules; `ARCHITECTURE.md` notes the duplication and why generating one from the other was not worth the machinery at this size.

The form posts directly to the backend through a typed `submitApplication` function in `lib/api.ts`. The backend enables CORS for the frontend origin in development. Loading, success, and error states are all rendered; failures surface the `ProblemDetails` title.

shadcn components used: `form`, `input`, `select`, `button`, `card`, `alert`, `sonner`.

## 11. Testing

`Fundo.LoanApp.UnitTests` — xUnit, no I/O:

- `RestrictedStateRule` denies `NY` and passes `TX`.
- `BlacklistedSsnRule` denies a blacklisted hash and passes an unknown one.
- `DecisionEngine` returns the first denial and does not evaluate later rules.
- `DecisionEngine` approves when every rule passes.
- `HmacSsnHasher` is deterministic, normalizes formatting differences, and produces different hashes under different keys.
- `SubmitLoanApplicationHandler` with fakes: denial persists nothing and publishes nothing; approval publishes exactly one event; a failure inside the transaction publishes nothing.
- NetArchTest: Domain has no outward dependencies.

`Fundo.LoanApp.IntegrationTests` — xUnit, `WebApplicationFactory`, Testcontainers PostgreSQL:

- `POST /api/applications` with a clean SSN returns `201` and inserts exactly one customer and one application.
- The same SSN posted twice with different data returns `201`, still leaves one customer and one application, and reflects the updated values.
- `POST` with `state: "NY"` returns `200 Denied` and leaves both tables empty.
- `POST` with a blacklisted SSN returns `200 Denied` and leaves both tables empty.
- A forced failure after the customer insert leaves both tables empty, proving the rollback.
- `GET /api/applications/{id}` returns the masked SSN and never the full value.

Each test class gets its own container-backed database, so tests do not share state.

## 12. Local setup

`docker-compose.yml` starts PostgreSQL 17 and the mock service. The API and the frontend run from the shell so logs stay readable during the demo:

```
docker compose up -d
dotnet run --project backend/src/Fundo.LoanApp.Api
pnpm --dir frontend dev
```

Migrations and seeding run automatically at API startup. `README.md` documents the ports, the test SSNs for each scenario, and the test commands.

## 13. Trade-offs

| Decision | Alternative | Why this one |
|---|---|---|
| In-process channel for events | Transactional outbox | Meets "processed in the background" without a table, a poller, and idempotency handling. Limitation documented. |
| One customer to one application | One customer to many applications | The challenge says update the existing records rather than create duplicates. The unique index makes the assumption explicit and enforced. |
| HMAC-hashed SSN | Plaintext SSN column | Keeps the full SSN out of the database while preserving a single indexed lookup. |
| `200 OK` for denials | `422 Unprocessable Entity` | A denial is a valid outcome of a well-formed request. |
| Zod schema duplicated from FluentValidation | Generated client types | At six fields, generation costs more than it saves. |
| Testcontainers | Shared local database | Real transactions, real Postgres, no shared state between test classes, and no setup step for the reviewer beyond Docker. |

## 14. Documentation deliverables

`README.md` — demo video link at the top, prerequisites, run commands for database, mock service, backend, and frontend, test commands, and a test-data table mapping each SSN and state to its expected outcome.

`ARCHITECTURE.md` — project structure and layer responsibilities, how the rule engine works and how to add a rule, the background event workflow and external service contract, the transaction boundary and failure behavior, and the trade-offs and omissions from sections 2.3 and 13.
