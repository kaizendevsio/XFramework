using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Infrastructure;

public sealed class IdentityServerTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue(TestAuthHeaders.Unauthenticated, out var unauthenticatedHeader) &&
            bool.TryParse(unauthenticatedHeader.FirstOrDefault(), out var unauthenticated) &&
            unauthenticated)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var tenantId = TryGetGuidHeader(TestAuthHeaders.TenantId, out var suppliedTenantId)
            ? suppliedTenantId
            : IntegrationTestFixture.TestTenantId;
        var credentialId = TryGetGuidHeader(TestAuthHeaders.CredentialId, out var suppliedCredentialId)
            ? suppliedCredentialId
            : IntegrationTestFixture.TestCredentialId;

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, credentialId.ToString("D")),
            new(ClaimTypes.Name, "identityserver-test-admin"),
            new("credential_id", credentialId.ToString("D")),
            new("tenant_id", tenantId.ToString("D")),
            new(ClaimTypes.Role, "SuperAdmin")
        ];

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private bool TryGetGuidHeader(string headerName, out Guid value)
    {
        value = Guid.Empty;
        return Request.Headers.TryGetValue(headerName, out var header) &&
               Guid.TryParse(header.FirstOrDefault(), out value);
    }
}
