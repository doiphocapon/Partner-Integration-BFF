namespace PartnerIntegrationBFF.Api.Options;

public sealed class PartnerVerificationOptions
{
    public const string SectionName = "PartnerVerification";

    public required string BaseUrl { get; init; }

    public int MaxRetryAttempts { get; init; } = 3;

    public int RetryBaseDelayMilliseconds { get; init; } = 200;

    public int AttemptTimeoutMilliseconds { get; init; } = 2000;
}
