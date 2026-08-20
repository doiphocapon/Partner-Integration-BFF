using Microsoft.AspNetCore.Authentication;

namespace PartnerIntegrationBFF.Api.Security;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
}
