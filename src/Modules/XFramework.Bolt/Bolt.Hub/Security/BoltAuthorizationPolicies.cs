using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using XFramework.Domain.Shared.ServiceIdentity;

namespace Bolt.Hub.Security;

public static class BoltAuthorizationPolicies
{
    public const string ServiceDiscoveryReader = "BoltServiceDiscoveryReader";

    public static void AddServiceDiscoveryReaderPolicy(AuthorizationOptions options) =>
        options.AddPolicy(ServiceDiscoveryReader, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => IsServiceDiscoveryReader(context.User));
        });

    public static bool IsServiceDiscoveryReader(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true &&
        (HasScope(user, XFrameworkServiceScopes.BoltService) ||
         HasAdminScope(user) ||
         user.IsInRole("Admin"));

    private static bool HasAdminScope(ClaimsPrincipal user) =>
        XFrameworkServiceScopes.AdminDefaults
            .Where(static scope => scope.EndsWith(".admin", StringComparison.Ordinal))
            .Any(scope => HasScope(user, scope));

    private static bool HasScope(ClaimsPrincipal user, string requiredScope) =>
        user.FindAll("scope")
            .Concat(user.FindAll("scp"))
            .SelectMany(static claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(scope => string.Equals(scope, requiredScope, StringComparison.OrdinalIgnoreCase));
}
