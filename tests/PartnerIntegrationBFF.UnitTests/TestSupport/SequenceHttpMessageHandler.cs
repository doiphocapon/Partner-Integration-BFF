namespace PartnerIntegrationBFF.UnitTests.TestSupport;

/// <summary>
/// Replays a fixed sequence of responses/exceptions per call, so retry behaviour can be
/// asserted deterministically without any real network activity or wall-clock timing.
/// </summary>
public sealed class SequenceHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _steps;

    public SequenceHttpMessageHandler(IEnumerable<Func<HttpResponseMessage>> steps)
    {
        _steps = new Queue<Func<HttpResponseMessage>>(steps);
    }

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;

        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("No more steps configured for the fake handler.");
        }

        return Task.FromResult(_steps.Dequeue()());
    }
}
