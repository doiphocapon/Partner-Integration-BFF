namespace PartnerIntegrationBFF.Api.Services;

public enum PartnerVerificationStatus
{
    Verified,
    NotVerified,
    ServiceUnavailable,
}

public sealed record PartnerVerificationResult(PartnerVerificationStatus Status)
{
    public static PartnerVerificationResult Verified() => new(PartnerVerificationStatus.Verified);

    public static PartnerVerificationResult NotVerified() => new(PartnerVerificationStatus.NotVerified);

    public static PartnerVerificationResult Unavailable() => new(PartnerVerificationStatus.ServiceUnavailable);
}
