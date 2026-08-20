using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using PartnerIntegrationBFF.Api.Contracts;
using PartnerIntegrationBFF.Api.Services;
using PartnerIntegrationBFF.UnitTests.TestSupport;
using Polly;

namespace PartnerIntegrationBFF.UnitTests.Services;

public class HttpPartnerVerificationServiceTests
{
    private const int MaxRetryAttempts = 3;

    [Fact]
    public async Task VerifyAsync_FirstCallSucceeds_ReturnsVerifiedWithoutRetrying()
    {
        var handler = new SequenceHttpMessageHandler(new Func<HttpResponseMessage>[]
        {
            () => VerifiedResponse(isVerified: true),
        });

        var sut = CreateService(handler);

        var result = await sut.VerifyAsync("P-1001", CancellationToken.None);

        result.Status.Should().Be(PartnerVerificationStatus.Verified);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task VerifyAsync_TransientFailureThenSuccess_RetriesOnceAndReturnsVerified()
    {
        var handler = new SequenceHttpMessageHandler(new Func<HttpResponseMessage>[]
        {
            () => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            () => VerifiedResponse(isVerified: true),
        });

        var sut = CreateService(handler);

        var result = await sut.VerifyAsync("P-1001", CancellationToken.None);

        result.Status.Should().Be(PartnerVerificationStatus.Verified);
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task VerifyAsync_TwoTransientFailuresThenSuccess_RetriesTwiceAndReturnsVerified()
    {
        var handler = new SequenceHttpMessageHandler(new Func<HttpResponseMessage>[]
        {
            () => throw new HttpRequestException("simulated transient failure 1"),
            () => throw new HttpRequestException("simulated transient failure 2"),
            () => VerifiedResponse(isVerified: true),
        });

        var sut = CreateService(handler);

        var result = await sut.VerifyAsync("P-1001", CancellationToken.None);

        result.Status.Should().Be(PartnerVerificationStatus.Verified);
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task VerifyAsync_FailuresExceedMaxRetryAttempts_ReturnsServiceUnavailableAfterExhaustingRetries()
    {
        var handler = new SequenceHttpMessageHandler(Enumerable.Range(0, MaxRetryAttempts + 1)
            .Select<int, Func<HttpResponseMessage>>(_ => () => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var sut = CreateService(handler);

        var result = await sut.VerifyAsync("P-1001", CancellationToken.None);

        result.Status.Should().Be(PartnerVerificationStatus.ServiceUnavailable);
        // One initial attempt + MaxRetryAttempts retries, then give up.
        handler.CallCount.Should().Be(MaxRetryAttempts + 1);
    }

    [Fact]
    public async Task VerifyAsync_PartnerNotVerified_ReturnsNotVerifiedWithoutRetrying()
    {
        var handler = new SequenceHttpMessageHandler(new Func<HttpResponseMessage>[]
        {
            () => VerifiedResponse(isVerified: false),
        });

        var sut = CreateService(handler);

        var result = await sut.VerifyAsync("P-1001", CancellationToken.None);

        result.Status.Should().Be(PartnerVerificationStatus.NotVerified);
        handler.CallCount.Should().Be(1);
    }

    private static HttpPartnerVerificationService CreateService(SequenceHttpMessageHandler innerHandler)
    {
        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = MaxRetryAttempts,
                BackoffType = DelayBackoffType.Constant,
                UseJitter = false,
                Delay = TimeSpan.FromMilliseconds(1),
            })
            .Build();

        var httpClient = new HttpClient(new ResilienceDelegatingHandler(pipeline, innerHandler))
        {
            BaseAddress = new Uri("http://localhost/"),
        };

        return new HttpPartnerVerificationService(httpClient, NullLogger<HttpPartnerVerificationService>.Instance);
    }

    private static HttpResponseMessage VerifiedResponse(bool isVerified) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new PartnerVerificationResponse("P-1001", isVerified)),
    };
}
