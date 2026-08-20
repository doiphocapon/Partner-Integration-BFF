using Polly;

namespace PartnerIntegrationBFF.UnitTests.TestSupport;

/// <summary>
/// Wraps an inner handler with a Polly resilience pipeline, mirroring how
/// Microsoft.Extensions.Http.Resilience wires a pipeline into an HttpClient, so tests can
/// exercise the exact retry/timeout configuration used in Program.cs against a fake inner
/// handler instead of the network.
/// </summary>
public sealed class ResilienceDelegatingHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public ResilienceDelegatingHandler(ResiliencePipeline<HttpResponseMessage> pipeline, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        _pipeline = pipeline;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _pipeline
            .ExecuteAsync(ct => new ValueTask<HttpResponseMessage>(base.SendAsync(request, ct)), cancellationToken)
            .AsTask();
    }
}
