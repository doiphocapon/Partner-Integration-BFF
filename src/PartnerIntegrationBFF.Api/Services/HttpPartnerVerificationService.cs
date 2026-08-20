using System.Net.Http.Json;
using PartnerIntegrationBFF.Api.Contracts;
using Polly.Timeout;

namespace PartnerIntegrationBFF.Api.Services;

public sealed class HttpPartnerVerificationService : IPartnerVerificationService
{
    private const string VerificationPath = "api/v1/internal/partner-verification";

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpPartnerVerificationService> _logger;

    public HttpPartnerVerificationService(HttpClient httpClient, ILogger<HttpPartnerVerificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PartnerVerificationResult> VerifyAsync(string partnerId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                VerificationPath,
                new PartnerVerificationRequest(partnerId),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Partner verification for {PartnerId} returned non-success status {StatusCode} after retries.",
                    partnerId,
                    response.StatusCode);
                return PartnerVerificationResult.Unavailable();
            }

            var body = await response.Content.ReadFromJsonAsync<PartnerVerificationResponse>(cancellationToken);
            return body is { IsVerified: true }
                ? PartnerVerificationResult.Verified()
                : PartnerVerificationResult.NotVerified();
        }
        catch (Exception ex) when (IsTransientFailure(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Partner verification service unavailable for partner {PartnerId} after retries.", partnerId);
            return PartnerVerificationResult.Unavailable();
        }
    }

    private static bool IsTransientFailure(Exception exception, CancellationToken callerCancellationToken)
    {
        if (callerCancellationToken.IsCancellationRequested)
        {
            // The caller asked us to stop; this is not a partner-verification failure to swallow.
            return false;
        }

        return exception is HttpRequestException or TimeoutRejectedException or TaskCanceledException or TimeoutException;
    }
}
