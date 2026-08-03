using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using XFramework.TestInfrastructure;

namespace Storage.IntegrationTests.Infrastructure;

public sealed class StorageTestAuthHandler(
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
            : StorageIntegrationTestFixture.TestTenantId;
        var identityId = TryGetGuidHeader(TestAuthHeaders.IdentityId, out var suppliedIdentityId)
            ? suppliedIdentityId
            : Guid.Parse("00000000-0000-0000-0000-000000000690");
        var username = Request.Headers.TryGetValue(TestAuthHeaders.Username, out var usernameHeader)
            ? usernameHeader.FirstOrDefault() ?? "storage-test"
            : "storage-test";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identityId.ToString()),
            new(ClaimTypes.Name, username),
            new("identity_id", identityId.ToString()),
            new("tenantId", tenantId.ToString()),
            new("TenantId", tenantId.ToString()),
            new("tid", tenantId.ToString()),
            new(ClaimTypes.Role, "Admin")
        };

        var credentialId = TryGetGuidHeader(TestAuthHeaders.CredentialId, out var suppliedCredentialId)
            ? suppliedCredentialId
            : StorageIntegrationTestFixture.TestCredentialId;
        claims.Add(new Claim("credential_id", credentialId.ToString()));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private bool TryGetGuidHeader(string headerName, out Guid value)
    {
        value = Guid.Empty;
        return Request.Headers.TryGetValue(headerName, out var header) &&
               Guid.TryParse(header.FirstOrDefault(), out value);
    }
}
