using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PartnerIntegrationBFF.Api.Options;

namespace PartnerIntegrationBFF.Api.Security;

/// <summary>
/// Demonstrates securing the transaction endpoint with a shared API key rather than leaving it
/// open. A single static key is a starting point for this exercise, not a production posture —
/// see README for the recommended production path (per-partner keys or OAuth2 client credentials).
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string HeaderName = "X-Api-Key";

    private readonly IOptions<ApiKeySecurityOptions> _securityOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<ApiKeySecurityOptions> securityOptions)
        : base(options, logger, encoder)
    {
        _securityOptions = securityOptions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing '{HeaderName}' header."));
        }

        if (!string.Equals(providedKey.ToString(), _securityOptions.Value.ApiKey, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "partner-client") },
            Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized.",
            Detail = $"A valid '{HeaderName}' header is required.",
            Instance = Request.Path,
        };

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Response.WriteAsJsonAsync(problemDetails);
    }
}
