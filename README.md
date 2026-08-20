# Partner Integration BFF

A .NET 8 Web API that accepts partner transactions, validates them, verifies the partner through
an intentionally unreliable HTTP dependency, and reliably publishes accepted transactions to
RabbitMQ.

## Architecture

Single ASP.NET Core Web API project. The "external" partner verification dependency is an
internal endpoint in the same project, called over real HTTP through a typed `HttpClient` — this
matches the assessment's "same solution/project" requirement while still exercising genuine
network/resilience behaviour (see [`.agent/decisions.md`](.agent/decisions.md) #1 for the
reasoning and trade-off).

```
Client
  │  POST /api/v1/partner/transactions   (X-Api-Key header)
  ▼
ApiKeyAuthenticationHandler ──401──► ProblemDetails
  │ ok
  ▼
PartnerTransactionsController
  │
  ├─► FluentValidation (TransactionRequestValidator) ──invalid──► 400 ValidationProblemDetails
  │     ok
  ▼
IPartnerVerificationService (HttpPartnerVerificationService)
  │     typed HttpClient + Microsoft.Extensions.Http.Resilience
  │     (retry + per-attempt timeout, no circuit breaker)
  ▼
  POST /api/v1/internal/partner-verification   (dummy endpoint, ~30% throws, ~70% verified)
  │
  ├─ NotVerified ──────────────────────► 422 ProblemDetails
  ├─ ServiceUnavailable (exhausted) ───► 503 ProblemDetails
  │  Verified
  ▼
ITransactionPublisher (RabbitMqTransactionPublisher)
  │     publisher confirms, mandatory publish
  ├─ Failed ───────────────────────────► 503 ProblemDetails
  │  Published
  ▼
202 Accepted { transactionReference, correlationId, status }
```

Any unhandled exception anywhere in the pipeline is caught by `GlobalExceptionHandler` and
returned as a `500` ProblemDetails instead of leaking a stack trace or an ASP.NET default error
page.

### Project layout

```
PartnerIntegrationBFF.sln
├── src/PartnerIntegrationBFF.Api
│   ├── Controllers/        PartnerTransactionsController, PartnerVerificationController (dummy)
│   ├── Contracts/          Request/response DTOs
│   ├── Validation/         FluentValidation validator
│   ├── Services/           IPartnerVerificationService + HTTP implementation
│   ├── Messaging/          ITransactionPublisher + RabbitMQ implementation, connection provider
│   ├── Security/           API-key authentication handler
│   ├── ErrorHandling/      Global IExceptionHandler
│   └── Options/            Strongly-typed configuration
├── tests/PartnerIntegrationBFF.UnitTests           validation, resilience, publisher, controller
└── tests/PartnerIntegrationBFF.IntegrationTests    WebApplicationFactory endpoint tests
```

Persistent context for future work (requirements, ADRs, phase checklist, progress log) lives
under [`.agent/`](.agent) and [`CLAUDE.md`](CLAUDE.md).

## Running it

Requires the .NET 8 SDK (pinned via `global.json`; also usable via Docker without a local
install) and Docker for RabbitMQ.

```bash
# Build & test
dotnet build
dotnet test

# Run locally (RabbitMQ must be reachable at localhost:5672 with user/pass "app"/"app-dev-only-password",
# or override via RabbitMq__UserName / RabbitMq__Password)
dotnet run --project src/PartnerIntegrationBFF.Api

# Or run the whole stack (API + RabbitMQ) in Docker
docker compose up --build
```

Once running: Swagger UI at `/swagger` (Development environment), health check at `/health`.

Example request (replace the API key if you changed `Security:ApiKey` / `API_KEY`):

```bash
curl -X POST http://localhost:8080/api/v1/partner/transactions \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: local-dev-partner-key" \
  -d '{
    "partnerId": "P-1001",
    "transactionReference": "TXN-99823",
    "amount": 250.00,
    "currency": "USD",
    "timestamp": "2024-05-10T14:30:00Z"
  }'
```

Verified this end-to-end against a live `docker compose up` stack: `/health` returns `Healthy`,
a valid request returns `202 Accepted`, and the message is confirmed present on the
`partner-transactions` RabbitMQ queue via the management API.

## Testing

36 tests total (`dotnet test`), all deterministic — no `Thread.Sleep`, no real network calls, no
dependency on the dummy endpoint's actual randomness:

- **Unit** (`tests/PartnerIntegrationBFF.UnitTests`): required-field/amount/currency validation;
  partner verification retry/backoff/timeout behaviour proven with a fake `DelegatingHandler`
  wrapping a real (fast-delay) Polly pipeline — exact attempt counts asserted for immediate
  success, single-retry success, two-retry success, and retry-exhaustion; RabbitMQ publisher
  success/broker-rejection/connection-failure via NSubstitute mocks of `IConnection`/`IChannel`;
  controller-level gating (invalid/not-verified/unavailable/publish-failed requests never reach
  the publisher; a valid, verified request publishes exactly once with the right content).
- **Integration** (`tests/PartnerIntegrationBFF.IntegrationTests`): `WebApplicationFactory`-hosted
  tests through the real routing/auth/validation pipeline, substituting
  `IPartnerVerificationService` and `ITransactionPublisher` — 401 (missing/wrong API key), 400
  (validation), 422 (not verified), 503 (verification unavailable / broker failure), 202
  (accepted, correct body, publisher called exactly once).

The real HTTP verification call + resilience pipeline together, and the real RabbitMQ publish
path, are exercised manually via `docker compose up` (see above) rather than by an automated
test — see [`.agent/decisions.md`](.agent/decisions.md) #8 and #10 for why.

## Key decisions & trade-offs

Full ADR-style detail is in [`.agent/decisions.md`](.agent/decisions.md). Summary:

| Area | Choice | Trade-off |
|---|---|---|
| Verification API | Internal endpoint, same project, real HTTP | Less realistic than a separate service; matches assessment literally |
| Broker | RabbitMQ, publisher confirms | — |
| Resilience | `Microsoft.Extensions.Http.Resilience`, 3 retries, exp. backoff, no circuit breaker | No sustained-outage protection; unnecessary for this exercise |
| Validation | FluentValidation, called explicitly (not auto-filter) | Keeps validate→verify→publish flow explicit in the controller |
| Currency | Small hard-coded allowlist | Not full ISO-4217; documented as extensible via config |
| Response codes | 400 invalid / 422 not verified / 503 verification-or-broker-unavailable / 202 accepted | 422 vs 503 split distinguishes "rejected" from "couldn't tell" |
| Security | API key (`X-Api-Key`) | Not production-grade (no per-partner keys/rotation) — see below |
| Idempotency | Not implemented | Documented only — see below |
| Testing | Unit + `WebApplicationFactory`, no Testcontainers | RabbitMQ only exercised live via docker-compose |

## Production considerations (out of scope here, deliberately)

- **Idempotency**: `transactionReference` isn't deduplicated. A production version would reject
  or no-op a repeat of a reference already accepted (e.g. an idempotency store keyed on
  `partnerId + transactionReference`, or a unique constraint if a database were introduced).
- **Outbox pattern**: publishing happens synchronously in the request path. If the process
  crashed between the broker confirming the publish and the HTTP response being written, the
  caller could see a false failure for a transaction that actually queued. An outbox table plus a
  background dispatcher would remove that window at the cost of at-least-once delivery semantics
  and added infrastructure — reasonable for a production system, out of scope for this exercise.
- **Circuit breaker**: the verification client only retries transient failures with backoff; it
  doesn't trip a circuit under sustained outage. Worth adding if the verification dependency were
  a real, possibly-down-for-minutes external service.
- **Security**: the API key is a single static value from configuration. Production options:
  per-partner keys with rotation, or OAuth2 client-credentials/JWT bearer tokens issued by an
  identity provider.
- **RabbitMQ credentials**: local dev/docker-compose use a fixed non-`guest` user (RabbitMQ
  restricts the built-in `guest` account to loopback-only connections, which breaks cross-container
  auth) with a placeholder password sourced from environment variables with a documented default —
  a real deployment would source these from a secret store.

## Assumptions

- "Valid currency" means membership in a small configurable allowlist (USD, EUR, GBP, JPY, VND,
  AUD, CAD), not a full ISO-4217 lookup — the assessment says "must be valid", not "must be a
  well-formed 3-letter code".
- The dummy verification endpoint always returns `IsVerified: true` on its ~70% success path;
  its role is to simulate infrastructure unreliability (throws/timeouts), not partner risk
  logic. The 422 "not verified" contract path is proven via controller/integration tests that
  substitute the verification service directly.
- `guest`/`guest` (RabbitMQ's built-in default) is intentionally *not* used, even for local dev —
  see "Production considerations" above.
