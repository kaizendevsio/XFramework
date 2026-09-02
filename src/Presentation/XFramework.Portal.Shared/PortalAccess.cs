using System.Security.Claims;

namespace XFramework.Portal.Shared;

public static class PortalAccess
{
    public static readonly Guid AdminRoleTypeId = new("14524d87-582d-4af6-8d6c-4f58ffad34f5");

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
            && roleTypeId == AdminRoleTypeId;
    }
}
