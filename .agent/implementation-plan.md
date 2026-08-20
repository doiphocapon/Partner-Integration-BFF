# Implementation Plan

Status legend: `[ ]` not started, `[~]` in progress, `[x]` completed, `[!]` blocked.

## Phase 1 — Scaffolding

- [x] Install .NET 8 SDK locally, pin repo via `global.json`.
- [x] `git init`.
- [x] Solution + 3 projects created (`Api`, `UnitTests`, `IntegrationTests`), project references
      wired, added to `.sln`.
- [x] NuGet packages installed (FluentValidation.AspNetCore, Microsoft.Extensions.Http.Resilience,
      RabbitMQ.Client, Swashbuckle.AspNetCore, AspNetCore.HealthChecks.Rabbitmq;
      FluentAssertions, NSubstitute, Microsoft.AspNetCore.Mvc.Testing for tests).
- [x] `Directory.Build.props` (nullable, analyzers, warnings-as-errors), `.gitignore`.
- [x] Removed template boilerplate (WeatherForecast).
- [x] Agent docs created (`CLAUDE.md`, `.agent/requirements.md`, `.agent/decisions.md`,
      `.agent/implementation-plan.md`, `.agent/progress.md`).
- [x] Checkpoint: `dotnet build` succeeds, 0 warnings/errors.

## Phase 2 — Domain & validation

- [x] `TransactionRequest`/`TransactionAcceptedResponse` models (`decimal` amount,
      `DateTimeOffset` timestamp).
- [x] Currency allowlist (`SupportedCurrenciesOptions`, configurable via `appsettings.json`).
- [x] `TransactionRequestValidator` (FluentValidation): required fields, `amount > 0`, currency
      allowlist (case-insensitive).
- [x] ProblemDetails error shape conventions (`ValidationProblemDetailsFactory`).
- [x] Checkpoint: 16 unit tests for validator pass (`dotnet test`).

## Phase 3 — Partner verification

- [x] Dummy verification endpoint (`POST /api/v1/internal/partner-verification`) — ~30% throws
      `TimeoutException` (surfaces as transient 500), ~70% returns `IsVerified: true`.
- [x] `IPartnerVerificationService` + `HttpPartnerVerificationService` using a typed `HttpClient`;
      maps non-success/transient exceptions to `ServiceUnavailable` (never throws to the caller
      unless the caller's own cancellation token fired).
- [x] Resilience pipeline configured (retry: 3 attempts, exponential backoff + jitter; per-attempt
      timeout) via `Microsoft.Extensions.Http.Resilience`'s `AddResilienceHandler`, no circuit
      breaker.
- [x] Checkpoint: 5 new unit tests (21 total) prove successful verification, retry-after-transient-
      failure, success-after-retry, failure-after-retry-exhaustion (exact call counts via
      `SequenceHttpMessageHandler` + a real Polly pipeline with 1ms constant delay — no real
      wall-clock wait, no flakiness).

## Phase 4 — Messaging

- [x] `ITransactionPublisher` abstraction (`Task<PublishResult>`, not a bool, so failure carries a
      reason and can't be silently coerced to success).
- [x] `RabbitMqTransactionPublisher` using publisher confirms (`CreateChannelOptions` with
      `publisherConfirmationsEnabled`/`publisherConfirmationTrackingEnabled`, `mandatory: true`
      publish) — `BasicPublishAsync` awaits the broker's ack/nack, any failure/exception maps to
      `PublishResult.Failed`, never a false-positive `Published`.
- [x] `IRabbitMqConnectionProvider` — single long-lived connection shared by the publisher and
      the health check (avoids leaking a connection per publish/health-check call).
- [x] RabbitMQ connection/options via `IOptions<RabbitMqOptions>` bound from configuration; local
      dev/docker-compose use RabbitMQ's own well-known `guest`/`guest` default (loopback-only by
      broker default), no other hard-coded credentials/URLs.
- [x] Health check for RabbitMQ (`AddHealthChecks().AddRabbitMQ(...)`, reuses the same connection
      provider), mapped at `/health`.
- [ ] docker-compose RabbitMQ service — deferred to Phase 7 alongside the Dockerfile.
- [x] Checkpoint: 3 new unit tests (24 total) for publish-success, broker-rejects-message, and
      connection-unavailable, using NSubstitute mocks of `IConnection`/`IChannel` (no real broker
      needed for `dotnet test`).

## Phase 5 — Endpoint wiring & cross-cutting

- [x] `PartnerTransactionsController`: validate → verify → publish → map to response/ProblemDetails
      (400 validation, 422 not-verified, 503 verification-unavailable, 503 publish-failed, 202
      accepted).
- [x] Global exception handler (`GlobalExceptionHandler : IExceptionHandler`) → ProblemDetails for
      unhandled exceptions.
- [x] API-key authentication handler (`ApiKeyAuthenticationHandler`, `X-Api-Key` header) applied
      to `PartnerTransactionsController` only; 401 responses are ProblemDetails too.
- [x] Swagger/OpenAPI configuration incl. API key security definition/requirement (Microsoft.OpenApi
      v2 API — namespace is `Microsoft.OpenApi`, not `.Models`, and security requirements key off
      `OpenApiSecuritySchemeReference` rather than embedding a `Reference` on `OpenApiSecurityScheme`
      — differs from older Swashbuckle tutorials).
- [x] `/health` endpoint (self + RabbitMQ), anonymous access.
- [x] Checkpoint: `dotnet build` clean, `dotnet test` 24/24 green; manual smoke test via `curl`
      confirmed 401 (no/bad key), 400 (validation), and 503 (RabbitMQ down) all return the
      expected ProblemDetails shape.

## Phase 6 — Tests

- [x] Unit: required-field validation, amount validation, currency validation (16, Phase 2).
- [x] Unit: successful verification, retry-after-timeout, success-after-retry,
      failure-after-retry-exhaustion (deterministic attempt counts) (5, Phase 3).
- [x] Unit: RabbitMQ publish success/broker-rejects/connection-unavailable (3, Phase 4).
- [x] Unit: `PartnerTransactionsController` — invalid request / not-verified / verification-
      unavailable / publish-failure never call the publisher (or never call verification for
      invalid input); valid+verified publishes exactly once with correct message content (5,
      new).
- [x] Integration (`WebApplicationFactory`, `TransactionsApiFactory` substituting
      `IPartnerVerificationService` + `ITransactionPublisher`): 401 (missing/wrong API key), 400
      (validation, publisher never called), 422 (not verified), 503 (verification unavailable /
      broker publish failure), 202 (accepted + body shape + publish called exactly once) (7,
      new). Real HTTP verification pipeline is covered by unit tests, not re-exercised here — see
      `.agent/decisions.md` #10 for why.
- [x] Checkpoint: `dotnet build` 0 warnings/errors; `dotnet test` 36/36 green (29 unit + 7
      integration).

## Phase 7 — Docker & docs

- [x] `Dockerfile` (multi-stage build; `USER $APP_UID` non-root, matches the standard .NET 8
      container template).
- [x] `docker-compose.yml` (API + RabbitMQ, RabbitMQ healthcheck gating API startup via
      `depends_on: condition: service_healthy`; credentials/API key via env vars with local-dev
      defaults, no secrets hard-coded).
- [x] Switched RabbitMQ credentials off the built-in `guest` account (loopback-only by broker
      default, which breaks cross-container auth) to a dedicated `app` user — appsettings.json
      and docker-compose.yml updated together.
- [x] README (architecture + text request-flow diagram, project layout, run/test commands,
      decisions/trade-offs table, production considerations, assumptions).
- [x] `.dockerignore` added.
- [x] Checkpoint: `docker compose build` + `docker compose up -d` succeeded; `/health` returned
      `Healthy`; a real `POST /api/v1/partner/transactions` returned `202 Accepted` on the first
      attempt (partner verification passed) and the message was confirmed present
      (`messages: 1`, `publish: 1`) on the `partner-transactions` queue via the RabbitMQ
      management API; a live invalid-amount request returned `400` ProblemDetails. Stack torn
      down with `docker compose down` after verification.

## Phase 8 — Final verification

- [x] `dotnet build` at repo root: 0 Warning(s), 0 Error(s).
- [x] `dotnet test` at repo root: 36/36 passed (29 unit + 7 integration).
- [x] `docker compose up --build` (done in Phase 7): `/health` → `Healthy`; live `POST
      /api/v1/partner/transactions` → `202 Accepted` on first attempt; RabbitMQ management API
      confirmed the message present on `partner-transactions` (`messages: 1`, `publish: 1`); live
      invalid-amount request → `400` ProblemDetails. Stack torn down afterward.
- [x] Final consistency pass over `.agent/*` and `CLAUDE.md` — all phases marked complete, all
      decisions recorded, progress log current.
