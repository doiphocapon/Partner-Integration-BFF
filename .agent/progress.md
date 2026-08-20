# Progress Log

## Current phase

Phase 5 — Transaction endpoint wiring & cross-cutting (starting).

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

## Known issues

- None currently. Note: RabbitMQ.Client 7.x API (async-first: `CreateChannelAsync`,
  `BasicPublishAsync`, `CreateChannelOptions` for publisher confirms) differs substantially from
  the older 6.x synchronous API — if extending this code, check the installed package's XML docs
  rather than assuming older tutorials/StackOverflow answers apply.

## Remaining work

- Phases 5–8 per `.agent/implementation-plan.md`: endpoint wiring (controller, global exception
  handler, API-key auth, Swagger), remaining tests (controller/integration), Docker/README, final
  verification.

## Recommended next action

Proceed to Phase 5: `PartnerTransactionsController` wiring validate → verify → publish →
response/ProblemDetails, global `IExceptionHandler`, API-key auth handler, Swagger.
