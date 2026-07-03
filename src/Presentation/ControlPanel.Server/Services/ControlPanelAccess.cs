using System.Security.Claims;

namespace ControlPanel.Server.Services;

public static class ControlPanelAccess
{
    public static bool CanManageTenants(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (bool.TryParse(user.FindFirst(ControlPanelAuthClaims.IsSuperUser)?.Value, out var isSuperUser)
            && isSuperUser)
        {
            return true;
        }

        return Guid.TryParse(user.FindFirst(ControlPanelAuthClaims.RoleTypeId)?.Value, out var roleTypeId)
            && roleTypeId == ControlPanelBootstrapConstants.AdminRoleTypeId;
    }
}
