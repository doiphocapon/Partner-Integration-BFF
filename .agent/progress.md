# Progress Log

## Current phase

Phase 4 — RabbitMQ messaging (starting).

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

## Tests executed and results

- `dotnet build`: succeeded, 0 warnings, 0 errors.
- `dotnet test` (UnitTests): 21/21 passed — validation (16) + verification service resilience (5:
  immediate success, single retry then success, two retries then success, exhaustion after
  MaxRetryAttempts+1 calls, not-verified response). All deterministic, ~242ms total, no real
  network or meaningful wall-clock delay.

## Known issues

- None currently.

## Remaining work

- Phases 4–8 per `.agent/implementation-plan.md`: RabbitMQ messaging, endpoint wiring, remaining
  tests (controller/integration), Docker/README, final verification.

## Recommended next action

Proceed to Phase 4: `ITransactionPublisher` + RabbitMQ implementation with publisher confirms,
health check, docker-compose RabbitMQ service.
