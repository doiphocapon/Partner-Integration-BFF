# Requirements

## Original assessment (summary)

Build a .NET 8 Web API microservice ("Partner Integration BFF") that:

1. Exposes `POST /api/v1/partner/transactions` accepting `partnerId`, `transactionReference`,
   `amount`, `currency`, `timestamp`. Validates all fields required, `amount > 0`, valid
   `currency`. Consistent error responses with appropriate status codes.
2. Verifies `partnerId` via a dummy Partner Verification API (same solution) that simulates an
   unreliable dependency: ~30% of calls throw/timeout, ~70% succeed. Must call it over real HTTP,
   apply a resilience strategy (retry + graceful failure), never crash the incoming request on
   verification-service unavailability, and retries must be deterministic in tests.
3. On valid + verified transactions, publishes to a local message broker via an
   `ITransactionPublisher` abstraction. Must not report success if the broker didn't accept the
   message.
4. xUnit tests covering: required-field validation, amount validation, currency validation,
   successful verification, retry-after-timeout, success-after-retry, failure-after-exhaustion,
   invalid/unverified transactions not published, verified transactions published, meaningful
   controller/endpoint behaviour.
5. Bonus (where practical): Dockerfile, docker-compose (API + broker), global exception handling
   with ProblemDetails, a security demonstration, health checks, Swagger, README.

Full original text is in the initial conversation that produced
`C:\Users\doiph\.claude\plans\you-are-helping-me-dapper-clarke.md` (kept for reference; not
duplicated here).

## Confirmed functional requirements

- Endpoint: `POST /api/v1/partner/transactions`, controllers (not Minimal APIs).
- Validation: FluentValidation — all fields required, `amount > 0`, currency in a small allowlist
  (USD, EUR, GBP, JPY, VND, AUD, CAD).
- Verification: internal endpoint `POST /api/v1/internal/partner-verification` in the same API
  project, called over real HTTP via a typed `HttpClient` with
  `Microsoft.Extensions.Http.Resilience` (3 retries, exponential backoff + jitter, per-attempt
  timeout). Retries only on transient failures (`TimeoutException`/`HttpRequestException`), never
  on "partner not found" (non-transient, 404-style).
- Messaging: RabbitMQ via `RabbitMQ.Client`, `ITransactionPublisher` abstraction, publisher
  confirms so publish success is only reported when the broker actually accepted the message.
- Response: `202 Accepted` with `{ transactionReference, correlationId, status }` only after
  confirmed publish.
- Errors: RFC 7807 ProblemDetails via global `IExceptionHandler`. Distinct status codes for
  invalid request (400), partner not verified (422), verification service unavailable after
  retries (503), broker publish failure (503).
- Security demo: API key via `X-Api-Key` header, custom `AuthenticationHandler`.
- Health checks: self + RabbitMQ.
- Swagger/OpenAPI enabled.

## Bonus requirements selected for implementation

- Dockerfile + docker-compose (API + RabbitMQ).
- Global exception handling with ProblemDetails.
- API-key security demonstration.
- Health checks (API + RabbitMQ).
- Swagger/OpenAPI.
- README with architecture, flow, assumptions, commands, trade-offs.

## Explicitly excluded / deferred features

- Database of any kind.
- MediatR / CQRS / generic repository pattern.
- Outbox pattern (documented as a production reliability consideration only).
- JWT/OAuth authentication (API key demo chosen instead — full JWT infra is out of scope).
- Idempotency / duplicate `transactionReference` handling (documented as a production
  consideration only, not implemented).
- Testcontainers-based RabbitMQ integration tests (unit tests + `WebApplicationFactory` with a
  mocked publisher is the confirmed test scope; RabbitMQ is only exercised live via
  docker-compose).
- Circuit breaker in the resilience pipeline (retries + timeout only — a circuit breaker adds
  little value for a single-request exercise and complicates deterministic testing).

## Acceptance criteria

- `dotnet build` and `dotnet test` succeed with 0 warnings (warnings-as-errors enabled) and all
  tests green.
- All required test scenarios from the assessment (see list above) are covered and pass
  deterministically (no `Thread.Sleep`, no timing-dependent assertions).
- `docker compose up --build` starts the API and RabbitMQ; a real `POST
  /api/v1/partner/transactions` call results in a visible message on the RabbitMQ queue when
  verification succeeds, and a clean error response when it doesn't.
- `GET /health` reports the API and RabbitMQ connectivity status.
- README allows a new reader to build, run, and test the solution from a clean checkout.

## Important assumptions

- The dummy verification endpoint decides pass/fail via injectable randomness (not
  `Random.Shared` directly) so that automated tests can force deterministic failure sequences.
- "Valid currency" means membership in a small hard-coded allowlist, not a full ISO-4217 lookup
  library — documented as an intentional scope trade-off.
- The API is a single deployable; the "external" verification dependency being in-process is a
  literal reading of "same solution/project" from the assessment, confirmed with the user.
