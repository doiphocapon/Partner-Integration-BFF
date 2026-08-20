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

- [ ] `ITransactionPublisher` abstraction.
- [ ] `RabbitMqTransactionPublisher` using publisher confirms; typed failure result (not a bool)
      so callers can't misreport success.
- [ ] RabbitMQ connection/options via `IOptions<T>`, no hard-coded credentials/URLs.
- [ ] docker-compose RabbitMQ service.
- [ ] Health check for RabbitMQ.
- [ ] Checkpoint: unit tests for publish-success/publish-failure behaviour with a fake channel.

## Phase 5 — Endpoint wiring & cross-cutting

- [ ] `PartnerTransactionsController`: validate → verify → publish → map to response/ProblemDetails.
- [ ] Global exception handler (`IExceptionHandler`) → ProblemDetails for unhandled exceptions.
- [ ] API-key authentication handler + middleware wiring, applied to the public endpoint.
- [ ] Swagger/OpenAPI configuration (incl. API key security definition).
- [ ] `/health` endpoint (self + RabbitMQ).
- [ ] Checkpoint: `dotnet build` clean; manual smoke test via Swagger UI.

## Phase 6 — Tests

- [ ] Unit: required-field validation, amount validation, currency validation.
- [ ] Unit: successful verification, retry-after-timeout, success-after-retry,
      failure-after-retry-exhaustion (deterministic attempt counts).
- [ ] Unit: invalid/unverified transaction never reaches publisher (mock verify/publish
      interactions).
- [ ] Unit: verified + valid transaction is published exactly once with expected content.
- [ ] Integration (`WebApplicationFactory`): end-to-end happy path (202 + body shape), validation
      failure (400 ProblemDetails), verification failure paths, publisher failure path (503).
- [ ] Checkpoint: `dotnet test` all green, 0 warnings.

## Phase 7 — Docker & docs

- [ ] `Dockerfile` (multi-stage build, non-root user).
- [ ] `docker-compose.yml` (API + RabbitMQ, healthchecks, no hard-coded secrets in the file —
      via env vars with safe local defaults).
- [ ] README (architecture, request-flow diagram, assumptions, run/test commands, trade-offs,
      production considerations: outbox, idempotency, circuit breaker, JWT).
- [ ] Update `.agent/progress.md`.

## Phase 8 — Final verification

- [ ] `dotnet build` (clean).
- [ ] `dotnet test` (clean).
- [ ] `docker compose up --build`; manual `POST /api/v1/partner/transactions` against live
      RabbitMQ; confirm message on queue; confirm `/health` reports both API and RabbitMQ.
