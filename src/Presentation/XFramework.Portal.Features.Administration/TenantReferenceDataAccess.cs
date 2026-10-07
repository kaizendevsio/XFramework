using System.Security.Claims;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Portal.Shared;

namespace XFramework.Portal.Features.Administration;

public static class TenantReferenceDataAccess
{
    public static async Task<bool> CanManageAsync(
        ClaimsPrincipal? user,
        Guid tenantId,
        IIdentityServerServiceWrapper identityServer)
    {
        if (tenantId == Guid.Empty || user?.Identity?.IsAuthenticated != true) return false;
        if (bool.TryParse(user.FindFirst(PortalAuthClaims.IsSuperUser)?.Value, out var superUser) && superUser)
            return true;
        if (!Guid.TryParse(user.FindFirst(PortalAuthClaims.TenantId)?.Value, out var actorTenantId)
            || actorTenantId != tenantId
            || !Guid.TryParse(user.FindFirst(PortalAuthClaims.CredentialId)?.Value, out var credentialId)
            || credentialId == Guid.Empty)
            return false;

        var response = await identityServer.CheckCredentialCapability(new CheckCredentialCapabilityRequest
        {
            CredentialId = credentialId,
            ModuleKey = "identity",
            SubFeatureKey = "tenants",
            CapabilityKey = "manage",
            Metadata = new RequestMetadata { RequestedTenantId = tenantId }
        });
        return response.IsSuccess && response.Response is { IsAllowed: true } permission
            && permission.TenantId == tenantId && permission.CredentialId == credentialId;
    }
}
