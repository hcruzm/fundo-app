# Fundo Loan Application

A loan application system in three pieces: a Next.js form, a .NET API, and a mock external
service standing in for a third party. The API runs each submission through a rule engine,
writes the customer and the application in a single transaction, and publishes an event that
is delivered to the external service outside the HTTP request that answers the form. A new
applicant gets a customer and an application; a returning applicant — matched on the hashed
SSN — has those same two records updated in place rather than duplicated.

## Demo

> **TODO — replace with the Loom link before submitting.**

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0 |
| Node.js | 20.9 or newer |
| pnpm | 10 (the repository pins `pnpm@10.33.4`) |
| Docker | any recent version, with Compose v2 |

## Running it locally

From the repository root, in this order. The API and the frontend each keep a terminal.

```bash
# 1. PostgreSQL (host port 5433) and the mock external service (port 5100)
docker compose up -d

# 2. Backend API on http://localhost:5080
dotnet run --project backend/src/Fundo.LoanApp.Api

# 3. Frontend dependencies (first run only)
pnpm --dir frontend install

# 4. Frontend on http://localhost:3000
pnpm --dir frontend dev
```

| Service | URL | Notes |
|---|---|---|
| PostgreSQL | `localhost:5433` | Published on 5433 so it does not collide with a locally installed PostgreSQL. The container still listens on 5432 internally. |
| Mock external service | `http://localhost:5100` | Runs in Docker. In-memory state; it resets when the container restarts. Because that state outlives `docker compose down -v` (which only clears PostgreSQL), a customer can be new to the database but already known to the mock, causing the background delivery to receive a `409`; run `docker compose restart mock-service` before a fresh walkthrough to reset it too. |
| Backend API | `http://localhost:5080` | |
| API reference (Scalar) | `http://localhost:5080/scalar/v1` | Served in the Development environment only. |
| Frontend | `http://localhost:3000` | Reads the API base URL from `frontend/.env.local`. |

Migrations and the blacklist seed run automatically when the API starts — `DatabaseInitializer`
calls `Database.MigrateAsync()` and then seeds `blacklisted_ssns`. There is no manual database
step, and the seed is idempotent, so restarting the API is safe.

To stop everything: `docker compose down` (add `-v` to drop the database volume as well).

## Tests

```bash
# Everything: 68 tests. Requires Docker.
dotnet test backend/Fundo.LoanApp.sln

# Fast suite: 44 unit tests, no Docker, no I/O.
dotnet test backend/tests/Fundo.LoanApp.UnitTests

# Integration suite: 24 tests. Requires Docker.
dotnet test backend/tests/Fundo.LoanApp.IntegrationTests
```

The integration tests need Docker because they start a real PostgreSQL container through
Testcontainers and run the migrations against it. The EF Core in-memory provider is not used
anywhere — the transaction and rollback tests would prove nothing against it. They run fully
isolated from the mock service on port 5100: the test host overrides `ExternalService:BaseUrl`
to a dead port, so no integration test ever calls it.

Frontend checks:

```bash
pnpm --dir frontend lint
pnpm --dir frontend build
```

## Test data

`111-11-1111` and `222-22-2222` are the SSNs seeded into the blacklist at startup. Any other
SSN is a new customer the first time it is submitted and a returning customer after that.

| Scenario | SSN | State | Expected |
|---|---|---|---|
| Approved, new customer | `123-45-6789` | `TX` | `201 Created`, `Approved`, `isReturningCustomer: false` |
| Returning customer | `123-45-6789` (submit twice) | `TX` | `201 Created`, `Approved`, `isReturningCustomer: true`, same ids, one row per table |
| Denied, restricted state | any | `NY` | `200 OK`, `Denied`, `reason: "We do not currently accept applications from NY."` |
| Denied, blacklisted SSN | `111-11-1111` or `222-22-2222` | `TX` | `200 OK`, `Denied`, `reason: "This application cannot be processed."` |

A denial is the correct answer to a well-formed request, so it is `200 OK` with a
`"decision": "Denied"` body rather than a 4xx. Nothing is written and no event is published
on a denial.

A submission from the command line:

```bash
curl -i -X POST http://localhost:5080/api/applications \
  -H 'Content-Type: application/json' \
  -d '{
    "firstName": "Ada",
    "lastName": "Lovelace",
    "companyName": "Analytical Engines LLC",
    "requestedAmount": 25000.00,
    "ssn": "123-45-6789",
    "address": { "street": "1 Byron Street", "city": "Austin", "state": "TX", "postalCode": "78701" }
  }'
```

Run it a second time with a different amount to see the returning-customer path:
`isReturningCustomer` becomes `true`, the `applicationId` and `customerId` are unchanged, and
the row counts stay at one per table.

## Verifying the external service

The mock service keeps everything it received in memory and exposes it:

```bash
curl -s http://localhost:5100/api/customers
```

After the two submissions above there is exactly **one** entry. Check that:

- `ssnHash` is a hex digest and `ssnLast4` is `6789` — the full SSN never leaves this system.
- `application.requestedAmount` is the **second** amount, which proves the `PUT` was delivered.
- `application.id` matches the `applicationId` the API returned.

The mock service also logs every call to stdout (`docker compose logs -f mock-service`), one
line per `CREATE` or `UPDATE`.

## API

| Method | Route | Response |
|---|---|---|
| `POST` | `/api/applications` | `201 Created` with `Location` on approval, `200 OK` on denial, `400` `ValidationProblemDetails` on a malformed body |
| `GET` | `/api/applications/{id}` | `200 OK` with the application, its customer and the masked SSN (`•••-••-6789`), `404` otherwise |
| `GET` | `/api/applications` | `200 OK` with every stored application, most recently updated first |
| `GET` | `/health` | `200 OK` |

Every non-success response is an RFC 9457 `ProblemDetails`. Unhandled exceptions return `500`
with a `correlationId` and no internal detail; the detail goes to the logs.

## Configuration

Backend settings live in `backend/src/Fundo.LoanApp.Api/appsettings.json` and
`appsettings.Development.json`. Every one of them can be overridden by an environment variable
using the standard `__` separator.

| Setting | Where | Value |
|---|---|---|
| `ConnectionStrings:Database` | `appsettings.Development.json` | `Host=localhost;Port=5433;Database=fundo_loans;Username=fundo;Password=fundo` |
| `Security:SsnHashKey` | `appsettings.Development.json` | Base64 key, at least 32 bytes, for the SSN HMAC |
| `Decision:RestrictedStates` | `appsettings.json` | `["NY"]` — the two-letter codes the `RestrictedStateRule` denies |
| `ExternalService:BaseUrl` | `appsettings.json` | `http://localhost:5100` |
| `Cors:AllowedOrigins` | `appsettings.json` | `["http://localhost:3000"]` |

The frontend reads one variable, `NEXT_PUBLIC_API_BASE_URL`, from `frontend/.env.local`. It is
committed so the project runs after a clone with no setup.

**On the SSN hash key.** The key in `appsettings.Development.json` is committed on purpose: it
is what lets a reviewer clone the repository and run it without generating anything first. It
is a development value and nothing else. A real deployment supplies its own key through the
environment and never stores it in the repository:

```bash
export Security__SsnHashKey="$(openssl rand -base64 32)"
```

Changing the key invalidates every stored hash — existing customers stop matching, and so do
the seeded blacklist entries — so in a real system it is a rotation problem, not a setting.

## Further reading

[`ARCHITECTURE.md`](ARCHITECTURE.md) covers the layer boundaries and how they are enforced, the
rule engine and how to add a rule, the background event path and its known failure window, the
transaction boundary, the SSN hashing decision, and everything that was deliberately left out.

