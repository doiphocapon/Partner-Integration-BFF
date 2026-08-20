# Architecture Decision Record

## 1. Solution structure — single API project

- **Decision**: One API project hosts both the public transaction endpoint and the internal
  dummy verification endpoint; two test projects (unit, integration).
- **Options considered**: (a) single project, (b) separate verification API project in the same
  solution.
- **Selected**: (a).
- **Reason**: Assessment says "same solution/project" for the verification API; keeps deployment
  and docker-compose simple; still exercises real HTTP + resilience since the call goes over
  the network stack via a typed `HttpClient`, not an in-process method call.
- **Trade-off**: Less realistic than a truly separate service/process; acceptable for exercise
  scope, called out in README.
- **Phase**: 1 (confirmed with user before implementation).

## 2. Message broker — RabbitMQ

- **Decision**: RabbitMQ, official Docker image, `RabbitMQ.Client` v7.
- **Options considered**: RabbitMQ, Kafka, Azure Service Bus emulator.
- **Selected**: RabbitMQ.
- **Reason**: Simplest local setup via Docker, most common "local message broker" expectation for
  this kind of assessment, first-class .NET client library.
- **Trade-off**: None significant for this scope.
- **Phase**: 1.

## 3. Resilience strategy — Microsoft.Extensions.Http.Resilience, no circuit breaker

- **Decision**: Standard resilience handler on the typed `HttpClient` for the verification
  service: per-attempt timeout, 3 retries with exponential backoff + jitter, total-request
  timeout. No circuit breaker.
- **Options considered**: raw Polly policies, `Microsoft.Extensions.Http.Resilience`, hand-rolled
  retry loop.
- **Selected**: `Microsoft.Extensions.Http.Resilience` (wraps Polly v8, integrates with
  `IHttpClientFactory`).
- **Reason**: Standard, testable via `DelegatingHandler` injection, avoids hand-rolled retry bugs.
  Circuit breaker adds state and complicates deterministic single-request tests without adding
  meaningful value at this scope.
- **Trade-off**: A circuit breaker would better protect a real system under sustained partner-API
  outage; documented as a production follow-up in README.
- **Phase**: 3.

## 4. Validation — FluentValidation

- **Decision**: FluentValidation validator for the transaction request, invoked explicitly before
  calling verification (not via automatic MVC filter, to keep control flow explicit).
- **Options considered**: DataAnnotations, FluentValidation, custom validator class.
- **Selected**: FluentValidation.
- **Reason**: Cleaner multi-rule composition (required + amount + currency), easy to unit test in
  isolation, idiomatic for "senior .NET" expectations.
- **Trade-off**: One extra dependency vs. built-in DataAnnotations; justified by test clarity.
- **Phase**: 2.

## 5. Currency validation — small allowlist

- **Decision**: Hard-coded allowlist (USD, EUR, GBP, JPY, VND, AUD, CAD) rather than the full
  ISO-4217 table or format-only (3-letter) validation.
- **Reason**: Assessment says "must be valid", not "must be well-formed"; a full ISO-4217 package
  is unnecessary ceremony for this scope; format-only would accept nonsense codes like "XXX".
- **Trade-off**: Real-world partners may use currencies outside the list; allowlist is easily
  extended via configuration — documented in README.
- **Phase**: 2.

## 6. API response semantics

- **Decision**: `202 Accepted` with `{ transactionReference, correlationId, status: "Accepted" }`
  only after the broker confirms the publish. `400` ProblemDetails for validation failures, `422`
  for a verified-but-rejected partner (partner check ran, came back negative), `503` ProblemDetails
  for verification-service exhaustion or broker publish failure (both are "try again later"
  conditions, not client error).
- **Reason**: `202` communicates "accepted for async processing", matching the queue-based flow;
  `503` for both downstream failure modes keeps the contract simple and correctly signals
  retriable server-side conditions to the caller.
- **Trade-off**: Some might expect `400` for an unverified partner; `422`/`503` split was chosen
  to distinguish "partner definitively rejected" from "we couldn't tell" — documented in README.
- **Phase**: 2/5.

## 7. Security demonstration — API key

- **Decision**: Custom `AuthenticationHandler<ApiKeyAuthenticationOptions>` validating
  `X-Api-Key` against a configured value, applied to the public transaction endpoint only (not
  the internal dummy verification endpoint, which represents an external partner's own API).
- **Options considered**: API key, JWT bearer, documentation-only.
- **Selected**: API key.
- **Reason**: Demonstrates a real, working, testable auth mechanism within scope; JWT would need
  token issuance infrastructure that's tangential to the assessment's core focus.
- **Trade-off**: Not production-grade (no per-partner keys, no rotation) — documented as a
  starting point for OAuth2 client-credentials or mTLS in README.
- **Phase**: 5.

## 8. Testing strategy — unit + WebApplicationFactory, no Testcontainers

- **Decision**: xUnit + FluentAssertions + NSubstitute for unit tests (validation, retry
  determinism via a fake `DelegatingHandler`, publish-gating logic). `WebApplicationFactory`
  integration tests with the real validation/verification pipeline but a substituted
  `ITransactionPublisher`. No live-RabbitMQ tests in the automated suite.
- **Reason**: Confirmed with user — deterministic, fast, no Docker dependency for `dotnet test`
  to pass in any environment (including CI without Docker-in-Docker).
- **Trade-off**: The real RabbitMQ publish path is only verified manually via docker-compose, not
  by an automated test — documented in README as a manual verification step / a good candidate
  for a future Testcontainers-based test.
- **Phase**: 6.

## 9. .NET SDK strategy

- **Decision**: Install .NET 8 SDK locally (side-by-side with pre-existing .NET 10), pin the repo
  to it via `global.json` (`8.0.424`, `rollForward: latestFeature`).
- **Reason**: Confirmed with user; matches the assessment's explicit .NET 8 requirement and
  allows normal local `dotnet build`/`test`/`run` without depending on Docker for every command.
- **Trade-off**: None once installed.
- **Phase**: 1.

## 10. Integration tests substitute both external dependencies

- **Decision**: `WebApplicationFactory`-based tests (`TransactionsApiFactory`) substitute both
  `IPartnerVerificationService` and `ITransactionPublisher` at the DI boundary, rather than
  letting the request hit the real internal verification endpoint over HTTP.
- **Options considered**: (a) substitute both dependencies, (b) let the real HTTP call to the
  dummy verification endpoint happen and force determinism via a configurable failure
  probability set to 0/1 per test.
- **Selected**: (a).
- **Reason**: `WebApplicationFactory`'s in-memory `TestServer` doesn't listen on a real TCP port,
  but the typed `HttpClient` used by `HttpPartnerVerificationService` makes a real socket
  connection to `PartnerVerification:BaseUrl` — the two don't connect without extra plumbing
  (dynamically discovering the Kestrel port and rewriting config before the first request), which
  is disproportionate ceremony for this scope. The real HTTP + resilience pipeline is already
  proven deterministically by `HttpPartnerVerificationServiceTests` (unit); integration tests add
  value by proving controller/middleware wiring (auth, validation, status-code mapping, publish
  gating), which doesn't require re-exercising that HTTP round-trip.
- **Trade-off**: No automated test exercises the full request→self-HTTP-call→retry→controller
  path together. Mitigated by manual `curl` smoke testing (see `.agent/progress.md`) and the
  Phase 8 docker-compose end-to-end check.
- **Phase**: 6.

## 11. RabbitMQ credentials — dedicated `app` user, not `guest`

- **Decision**: Both `appsettings.json` and `docker-compose.yml` use a non-`guest` RabbitMQ user
  (`app` / a documented placeholder password), configurable via `RabbitMq__UserName` /
  `RabbitMq__Password` env vars.
- **Reason**: RabbitMQ's built-in `guest` account is restricted by the broker itself to
  loopback-only connections. In docker-compose, the API container connects to the `rabbitmq`
  container over the Docker network — not loopback from the broker's point of view — so `guest`/
  `guest` is rejected with "PLAIN login refused" even though it "looks like" a normal local-dev
  credential. Discovered and fixed during the Phase 7 live docker-compose smoke test.
- **Trade-off**: One more env var to keep in sync between the `rabbitmq` service definition and
  the `api` service's `RabbitMq__*` settings; documented in README as a local-dev placeholder,
  not a production credential.
- **Phase**: 7.
