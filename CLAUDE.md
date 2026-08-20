# CLAUDE.md

## Project purpose

Partner Integration BFF — a .NET 8 Web API that accepts partner transactions, validates them,
verifies the partner via an (intentionally unreliable) HTTP dependency, and reliably publishes
accepted transactions to RabbitMQ. Built as a senior .NET technical assessment (~2-3 hour scope).

**Read `.agent/requirements.md`, `.agent/decisions.md`, `.agent/implementation-plan.md`, and
`.agent/progress.md` before making any change.** They contain the confirmed scope, the reasoning
behind architectural choices, the phase checklist, and current status.

## Solution structure

```
PartnerIntegrationBFF.sln
├── src/PartnerIntegrationBFF.Api          # Web API: transaction endpoint, dummy verification
│                                           # endpoint, messaging, auth, health checks, Swagger
├── tests/PartnerIntegrationBFF.UnitTests  # xUnit unit tests (validation, resilience, gating)
└── tests/PartnerIntegrationBFF.IntegrationTests  # WebApplicationFactory endpoint tests
```

Pinned to .NET SDK 8.0.424 via `global.json` (machine also has .NET 10 installed — always use
`dotnet` from this repo root so the pin applies).

## Main architectural rules

- Single deployable API project; the "external" partner verification dependency is an internal
  endpoint in the same project called over real HTTP via a typed `HttpClient` — see
  `.agent/decisions.md` for why.
- Resilience (retry/backoff/timeout) lives only in the HTTP client pipeline
  (`Microsoft.Extensions.Http.Resilience`), never as ad-hoc retry loops.
- `ITransactionPublisher` is the only way application code talks to RabbitMQ. Never inject
  `IConnection`/`IChannel` outside its implementation.
- Never report success (`202 Accepted`) unless the broker has confirmed the publish.
- No database, no MediatR/CQRS, no outbox pattern, no generic repositories — out of scope,
  documented as production considerations only in the README.
- Nullable reference types + analyzers + warnings-as-errors are on solution-wide
  (`Directory.Build.props`). Do not suppress warnings inline without a comment explaining why.

## Build and test commands

```
dotnet build
dotnet test
docker compose up --build     # API + RabbitMQ
```

## Coding conventions

- Controllers (not Minimal APIs). `decimal` for money, `DateTimeOffset` for timestamps.
- `CancellationToken` propagated through every async call.
- RFC 7807 ProblemDetails for all error responses via the global `IExceptionHandler`.
- Structured logging only — never log full request payloads or the API key.

## Scope boundaries

- Do not add authentication beyond the API-key demo without discussion.
- Do not add idempotency/dedup logic — it's documented as a production consideration, not
  implemented (see `.agent/decisions.md`).
- Do not add Testcontainers-based broker tests — RabbitMQ is only exercised live via
  docker-compose, not in the automated test suite (see `.agent/decisions.md`).

## Warnings

- **Do not silently change confirmed architectural decisions** in `.agent/decisions.md`. If
  implementation reveals a new ambiguity or a reason to deviate, stop and ask first.
- **Do not commit, push, publish, or expose secrets** (API keys, connection strings) without
  explicit approval. Configuration values go through `appsettings.json`/environment variables /
  `IOptions<T>`, never hard-coded.
