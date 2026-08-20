namespace PartnerIntegrationBFF.Api.Options;

public sealed class ApiKeySecurityOptions
{
    public const string SectionName = "Security";

    public required string ApiKey { get; init; }
}
