# Progress Log

## Current phase

Complete. All 8 phases done; assessment implementation finished.

## Work completed

- Phase 1: environment inspected, .NET 8 SDK installed and pinned via `global.json`, solution +
  3 projects scaffolded, packages installed, agent docs created, committed
  (`7b6f40e Scaffold solution, projects, and agent docs`).
- Phase 2: `TransactionRequest`/`TransactionAcceptedResponse` contracts, `SupportedCurrenciesOptions`,
  `TransactionRequestValidator` (FluentValidation, case-insensitive currency allowlist),
  `ValidationProblemDetailsFactory`. Registered in `Program.cs` (options + scoped validator, no
  MVC auto-validation filter — validator is called explicitly by the controller in Phase 5).
  Committed (`556a2eb`).
- Phase 3: dummy `PartnerVerificationController` (30% simulated `TimeoutException`, 70% verified),
  `IPartnerVerificationService`/`HttpPartnerVerificationService` typed HTTP client, resilience
  pipeline (3 retries, exponential backoff + jitter, per-attempt timeout, no circuit breaker) via
  `Microsoft.Extensions.Http.Resilience`. Base URL config (`PartnerVerification:BaseUrl`) matches
  the local dev Kestrel port (`http://localhost:5095/`) since the "external" dependency is a
  loopback call to the same process — will be overridden via env var for Docker in Phase 7.

- Phase 4: `ITransactionPublisher`/`RabbitMqTransactionPublisher` (RabbitMQ.Client 7.x, async API)
  using publisher confirms so a broker nack/timeout/connection failure always yields
  `PublishResult.Failed(...)`, never a false `Published`. `IRabbitMqConnectionProvider` shares one
  long-lived connection between the publisher and the `/health` RabbitMQ check. Config via
  `RabbitMqOptions` (appsettings `RabbitMq` section; `guest`/`guest` local-dev default, documented
  as broker-restricted-to-loopback, not a real secret). Committed (`1ef6bb5` covers Phase 3; Phase
  4 commit follows this update).

## Tests executed and results

- `dotnet build`: succeeded, 0 warnings, 0 errors.
- `dotnet test` (UnitTests): 24/24 passed — validation (16), verification resilience (5), RabbitMQ
  publisher (3: broker accepts, broker rejects/throws, connection unavailable) — all via
  NSubstitute mocks of `IConnection`/`IChannel`, no real broker required.

- Phase 5: `PartnerTransactionsController` (validate → verify → publish → 202/400/422/503),
  `GlobalExceptionHandler`, `ApiKeyAuthenticationHandler` (`X-Api-Key`, ProblemDetails 401),
  Swagger API-key security definition, `/health` mapped. Manually smoke-tested with `curl`:
  401 without/with-wrong key, 400 ProblemDetails for invalid amount, 503 ProblemDetails when
  RabbitMQ is down (publish path). `Program` made `public partial` for `WebApplicationFactory`
  in Phase 6.

## Known issues

- None currently. Notes for future sessions:
  - RabbitMQ.Client 7.x API (async-first: `CreateChannelAsync`, `BasicPublishAsync`,
    `CreateChannelOptions` for publisher confirms) differs substantially from the older 6.x
    synchronous API — check the installed package's XML docs rather than assuming older
    tutorials/StackOverflow answers apply.
  - Microsoft.OpenApi v2 (pulled in by Swashbuckle.AspNetCore 10.x) moved types out of
    `Microsoft.OpenApi.Models` into `Microsoft.OpenApi`, and security requirements now key off
    `OpenApiSecuritySchemeReference` (constructed with the `OpenApiDocument` from
    `AddSecurityRequirement`'s factory callback) instead of an `OpenApiSecurityScheme` with an
    embedded `Reference` property.

- Phase 6: `PartnerTransactionsControllerTests` (5 unit tests, NSubstitute doubles for
  validator/verification/publisher — invalid/not-verified/unavailable/publish-failed never
  publish; valid+verified publishes exactly once with correct message content).
  `TransactionsApiFactory` (`WebApplicationFactory<Program>`) + 7 integration tests covering
  401/400/422/503/202. Integration tests substitute both `IPartnerVerificationService` and
  `ITransactionPublisher` (see decisions.md #10 for why the real self-HTTP verification call
  isn't re-exercised here). `dotnet test` at repo root: 36/36 green (29 unit + 7 integration).

- Phase 7: Dockerfile (multi-stage, `$APP_UID` non-root), docker-compose.yml (API + RabbitMQ,
  healthcheck-gated startup), `.dockerignore`, README. Fixed a real bug found only by running the
  stack: RabbitMQ's `guest` user is loopback-only, so cross-container auth from the `api`
  container failed until switched to a dedicated `app` user (decisions.md #11). Verified live:
  `docker compose up -d` → `/health` Healthy → real `POST /api/v1/partner/transactions` → `202`
  on first attempt → confirmed via RabbitMQ management API that the message landed on
  `partner-transactions` (`messages: 1`) → live 400 for an invalid request → `docker compose
  down`.

- Phase 8: final `dotnet build` (0 warnings/errors) and `dotnet test` (36/36 green) at the repo
  root, re-confirmed after the Phase 7 Docker changes. Live docker-compose verification was
  already done in Phase 7 (see above) and re-reviewed here — no further changes needed.

## Remaining work

None — all 8 phases in `.agent/implementation-plan.md` are complete. Everything is committed on
`master` (not pushed anywhere).

## Recommended next action

If resuming this project later: re-run `dotnet test` and `docker compose up` to reconfirm nothing
has drifted, then consider the "Production considerations" section in the README (idempotency,
outbox, circuit breaker, real secret management) as the natural next scope if extending this
beyond the assessment.
