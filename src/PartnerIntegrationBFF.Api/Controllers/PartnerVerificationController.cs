using Microsoft.AspNetCore.Mvc;
using PartnerIntegrationBFF.Api.Contracts;

namespace PartnerIntegrationBFF.Api.Controllers;

/// <summary>
/// Dummy internal endpoint simulating an unreliable third-party partner verification API:
/// ~30% of calls throw (surfacing as a transient 500) and ~70% succeed.
/// </summary>
[ApiController]
[Route("api/v1/internal/partner-verification")]
public sealed class PartnerVerificationController : ControllerBase
{
    private const double SimulatedFailureProbability = 0.3;

    [HttpPost]
    public ActionResult<PartnerVerificationResponse> Verify([FromBody] PartnerVerificationRequest request)
    {
        if (Random.Shared.NextDouble() < SimulatedFailureProbability)
        {
            throw new TimeoutException($"Simulated partner verification timeout for partner '{request.PartnerId}'.");
        }

        return Ok(new PartnerVerificationResponse(request.PartnerId, IsVerified: true));
    }
}
