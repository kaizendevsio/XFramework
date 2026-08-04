using System.Net;
using System.Security.Claims;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Portal.Services;

public sealed class PortalAuthService(
    IIdentityServerServiceWrapper identityServer,
    ILogger<PortalAuthService> logger)
{
    public async Task<PortalLoginResult> AuthenticateAsync(
        string? username,
        string? password,
        bool rememberMe,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return PortalLoginResult.Failed("Username and password are required.");
        }

        var request = new AuthenticateIdentityRequest
        {
            UserName = username.Trim(),
            Password = password,
            RoleId = PortalBootstrapConstants.AdminRoleTypeId,
            AuthorizationType = AuthorizationType.Username,
            GenerateToken = true,
            RememberMe = rememberMe,
            Metadata = BuildMetadata(httpContext, PortalBootstrapConstants.AdminTenantId)
        };

        var response = await identityServer.AuthenticateIdentity(request);
        if (!response.IsSuccess || response.Response?.Credential is null || response.Response.Identity is null)
        {
            logger.LogWarning(
                "Portal login failed. Status={StatusCode}",
                (int)response.HttpStatusCode);

            var message = response.HttpStatusCode switch
            {
                HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.Forbidden =>
                    "Invalid username, password, or admin permission.",
                _ => "Unable to sign in. Check IdentityServer health and try again."
            };

            return PortalLoginResult.Failed(message);
        }

        if (string.IsNullOrWhiteSpace(response.Response.AccessToken))
            return PortalLoginResult.Failed("IdentityServer did not issue an actor token.");

        var principal = BuildPrincipal(
            response.Response,
            PortalBootstrapConstants.AdminRoleTypeId,
            PortalBootstrapConstants.AdminTenantId,
            username.Trim());
        return PortalLoginResult.Success(principal);
    }

    private static RequestMetadata BuildMetadata(HttpContext httpContext, Guid tenantId) => new()
    {
        RequestedTenantId = tenantId,
        RequestId = Guid.NewGuid(),
        OperationName = "Portal login",
        DeviceName = Environment.MachineName,
        UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
        IpAddress = httpContext.Connection.RemoteIpAddress?.ToString()
    };

    private static ClaimsPrincipal BuildPrincipal(
        IdentityServer.Domain.Shared.Contracts.Responses.AuthenticateIdentityResponse response,
        Guid roleTypeId,
        Guid tenantId,
        string username)
    {
        var identityId = response.Identity?.Id ?? Guid.Empty;
        var credentialId = response.Credential?.Id ?? Guid.Empty;
        var displayName = response.Identity?.FullName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = response.Identity?.IdentityName ?? username;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, credentialId.ToString()),
            new(ClaimTypes.Name, username),
            new(PortalAuthClaims.DisplayName, displayName),
            new(PortalAuthClaims.IdentityId, identityId.ToString()),
            new(PortalAuthClaims.CredentialId, credentialId.ToString()),
            new(PortalAuthClaims.TenantId, tenantId.ToString()),
            new(PortalAuthClaims.RoleTypeId, roleTypeId.ToString()),
            new(PortalAuthClaims.IsSuperUser, IsSuperUserRole(roleTypeId).ToString()),
            new(PortalAuthClaims.ActorAccessToken, response.AccessToken!)
        };

        if (response.SessionId is { } sessionId)
        {
            claims.Add(new Claim(PortalAuthClaims.SessionId, sessionId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, PortalAuthDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    private static bool IsSuperUserRole(Guid roleTypeId) =>
        roleTypeId == PortalBootstrapConstants.AdminRoleTypeId;

}

public sealed record PortalLoginResult(bool IsSuccess, string? Error, ClaimsPrincipal? Principal)
{
    public static PortalLoginResult Success(ClaimsPrincipal principal) => new(true, null, principal);
    public static PortalLoginResult Failed(string error) => new(false, error, null);
}

public static class PortalAuthDefaults
{
    public const string AuthenticationScheme = "PortalCookie";
}
