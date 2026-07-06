using System.Security.Claims;

namespace IdentityServer.Api.Features.Authorization.Shared;

internal static class IdentityAuthorizationEndpointMetadata
{
    public static void ApplyHttpContextActor(RequestMetadata metadata, HttpContext httpContext)
    {
        metadata.TenantId = ResolveGuidClaim(httpContext.User, "tenant_id", "tenantId", "TenantId", "tid", "tenant");
        metadata.CredentialId = ResolveGuidClaim(
            httpContext.User,
            "credentialId",
            "credential_id",
            ClaimTypes.NameIdentifier,
            "sub");
        metadata.ServiceAccessToken = null;
    }

    private static Guid? ResolveGuidClaim(ClaimsPrincipal user, params string[] claimNames)
    {
        foreach (var claimName in claimNames)
        {
            var claimValue = user.FindFirst(claimName)?.Value;
            if (Guid.TryParse(claimValue, out var value))
                return value;
        }

        return null;
    }
}
