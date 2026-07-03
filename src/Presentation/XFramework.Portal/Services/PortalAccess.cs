using System.Security.Claims;

namespace XFramework.Portal.Services;

public static class PortalAccess
{
    public static bool CanManageTenants(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (bool.TryParse(user.FindFirst(PortalAuthClaims.IsSuperUser)?.Value, out var isSuperUser)
            && isSuperUser)
        {
            return true;
        }

        return Guid.TryParse(user.FindFirst(PortalAuthClaims.RoleTypeId)?.Value, out var roleTypeId)
            && roleTypeId == PortalBootstrapConstants.AdminRoleTypeId;
    }
}
