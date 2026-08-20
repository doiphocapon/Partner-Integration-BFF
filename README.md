# Partner Integration BFF

A .NET 8 Web API that validates partner transactions, verifies partners through an intentionally unreliable HTTP dependency, and publishes accepted transactions to RabbitMQ.

## Architecture

```text
Client
  → API-key authentication
  → FluentValidation
  → Partner Verification API
      → Timeout and retry policies
  → RabbitMQ
      → Publisher confirmation
  → 202 Accepted
```

The mock Partner Verification API is an internal endpoint called over real HTTP using a typed `HttpClient`. It fails approximately 30% of requests to demonstrate retry and graceful failure handling.

Errors are returned consistently using RFC 7807 Problem Details.

## Technology

* .NET 8 ASP.NET Core Web API
* FluentValidation
* `Microsoft.Extensions.Http.Resilience`
* RabbitMQ with publisher confirmations
* xUnit and `WebApplicationFactory`
* Docker Compose
* Swagger/OpenAPI

## Run with Docker

```bash
docker compose up --build -d
```

Open:

| Service             | URL                           |
| ------------------- | ----------------------------- |
| Swagger             | http://localhost:8080/swagger |
| Health check        | http://localhost:8080/health  |
| RabbitMQ Management | http://localhost:15672        |

Local RabbitMQ credentials:

```text
Username: app
Password: app-dev-only-password
```

Stop the environment:

```bash
docker compose down
```

## Test through Swagger

Open Swagger and use **Authorize** with:

```text
local-dev-partner-key
```

Execute:

```text
POST /api/v1/partner/transactions
```

Example request:

```json
{
  "partnerId": "P-1001",
  "transactionReference": "TXN-99823",
  "amount": 250.00,
  "currency": "USD",
  "timestamp": "2024-05-10T14:30:00Z"
}
```

A verified and queued transaction returns:

```http
202 Accepted
```

```json
{
  "transactionReference": "TXN-99823",
  "correlationId": "...",
  "status": "Accepted"
}
```

Because the mock verification API deliberately fails approximately 30% of the time, some requests may require retries. If all retry attempts fail, the API returns `503 Service Unavailable`.

## Run the tests

```bash
dotnet build
dotnet test
```

The solution includes deterministic unit and integration tests covering:

* Required fields, amount, and currency validation.
* Verification success, retry, timeout, and retry exhaustion.
* RabbitMQ publish success and failure.
* Authentication and response codes.
* Valid transactions being published exactly once.
* Invalid or unverified transactions not being published.
* Global exception handling.

Unit tests do not require RabbitMQ or real network calls.

## Response codes

| Status | Meaning                                      |
| ------ | -------------------------------------------- |
| `202`  | Transaction verified and queued              |
| `400`  | Validation failed                            |
| `401`  | API key missing or invalid                   |
| `422`  | Partner not verified                         |
| `503`  | Verification service or RabbitMQ unavailable |
| `500`  | Unexpected error handled globally            |

## Key decisions

* The verification endpoint is in the same project but is called over real HTTP.
* Transient verification failures use timeout and exponential retry policies.
* RabbitMQ publisher confirmations prevent false success responses.
* API-key authentication demonstrates endpoint security.
* `202 Accepted` indicates that RabbitMQ confirmed the message.
* Errors use RFC 7807 Problem Details.

Detailed decisions and trade-offs are documented in [`.agent/decisions.md`](.agent/decisions.md).

## Project structure

```text
src/PartnerIntegrationBFF.Api
tests/PartnerIntegrationBFF.UnitTests
tests/PartnerIntegrationBFF.IntegrationTests
docker-compose.yml
```

## Production considerations

For production, the following should be considered:

* Idempotency using `partnerId + transactionReference`.
* Transactional outbox for stronger delivery guarantees.
* Circuit breaker for sustained verification outages.
* OAuth2/JWT or per-partner API keys.
* Managed secret storage.
* OpenTelemetry metrics and distributed tracing.

These were deliberately excluded to keep the solution proportionate to the expected 2–3-hour exercise.
