namespace PartnerIntegrationBFF.Api.Services;

public interface IPartnerVerificationService
{
    Task<PartnerVerificationResult> VerifyAsync(string partnerId, CancellationToken cancellationToken);
}
