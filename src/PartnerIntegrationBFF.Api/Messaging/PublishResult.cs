namespace PartnerIntegrationBFF.Api.Messaging;

public enum PublishStatus
{
    Published,
    Failed,
}

public sealed record PublishResult(PublishStatus Status, string? Error = null)
{
    public static PublishResult Published() => new(PublishStatus.Published);

    public static PublishResult Failed(string error) => new(PublishStatus.Failed, error);
}
