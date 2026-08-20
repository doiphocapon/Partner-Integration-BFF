using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using PartnerIntegrationBFF.Api.Contracts;
using PartnerIntegrationBFF.Api.Messaging;
using PartnerIntegrationBFF.Api.Services;
using PartnerIntegrationBFF.IntegrationTests.TestSupport;

namespace PartnerIntegrationBFF.IntegrationTests.Transactions;

public class PartnerTransactionsEndpointTests : IDisposable
{
    private const string Endpoint = "api/v1/partner/transactions";
    private const string ValidApiKey = "local-dev-partner-key";

    private readonly TransactionsApiFactory _factory = new();

    private static TransactionRequest ValidRequest() => new()
    {
        PartnerId = "P-1001",
        TransactionReference = "TXN-99823",
        Amount = 250.00m,
        Currency = "USD",
        Timestamp = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Post_WithoutApiKey_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Endpoint, ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_WithWrongApiKey_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.PostAsJsonAsync(Endpoint, ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_InvalidRequest_ReturnsBadRequestAndNeverPublishes()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(Endpoint, ValidRequest() with { Amount = -5 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey(nameof(TransactionRequest.Amount));

        await _factory.Publisher.DidNotReceive().PublishAsync(Arg.Any<TransactionMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_PartnerNotVerified_ReturnsUnprocessableEntityAndNeverPublishes()
    {
        _factory.VerificationService
            .VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PartnerVerificationResult.NotVerified());

        using var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(Endpoint, ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        await _factory.Publisher.DidNotReceive().PublishAsync(Arg.Any<TransactionMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_VerificationServiceUnavailable_ReturnsServiceUnavailableAndNeverPublishes()
    {
        _factory.VerificationService
            .VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PartnerVerificationResult.Unavailable());

        using var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(Endpoint, ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.Publisher.DidNotReceive().PublishAsync(Arg.Any<TransactionMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_VerifiedButBrokerRejects_ReturnsServiceUnavailable()
    {
        _factory.VerificationService
            .VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PartnerVerificationResult.Verified());
        _factory.Publisher
            .PublishAsync(Arg.Any<TransactionMessage>(), Arg.Any<CancellationToken>())
            .Returns(PublishResult.Failed("broker unavailable"));

        using var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(Endpoint, ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Post_ValidAndVerified_ReturnsAcceptedAndPublishesExactlyOnce()
    {
        _factory.VerificationService
            .VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PartnerVerificationResult.Verified());
        _factory.Publisher
            .PublishAsync(Arg.Any<TransactionMessage>(), Arg.Any<CancellationToken>())
            .Returns(PublishResult.Published());

        using var client = CreateAuthenticatedClient();
        var request = ValidRequest();

        var response = await client.PostAsJsonAsync(Endpoint, request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<TransactionAcceptedResponse>();
        body!.TransactionReference.Should().Be(request.TransactionReference);
        body.Status.Should().Be("Accepted");
        body.CorrelationId.Should().NotBeNullOrWhiteSpace();

        await _factory.Publisher.Received(1).PublishAsync(Arg.Any<TransactionMessage>(), Arg.Any<CancellationToken>());
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        return client;
    }

    public void Dispose() => _factory.Dispose();
}
