using System.Net;
using System.Security.Claims;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;

namespace ControlPanel.Server.Services;

public sealed class ControlPanelAuthService(
    IDataContext dataContext,
    IIdentityServerServiceWrapper identityServer,
    IConfiguration configuration,
    RequestMetadata requestMetadata,
    ILogger<ControlPanelAuthService> logger)
{
    public async Task<ControlPanelLoginResult> AuthenticateAsync(
        string? username,
        string? password,
        bool rememberMe,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return ControlPanelLoginResult.Failed("Username and password are required.");
        }

        var options = new ControlPanelAuthOptions();
        configuration.GetSection(ControlPanelAuthOptions.BootstrapAdminSectionName).Bind(options);

        var tenant = await dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => !x.IsDeleted)
            .Where(x => x.Name == options.TenantName)
            .FirstOrDefaultAsync(ct);

        if (tenant is null)
        {
            return ControlPanelLoginResult.Failed("ControlPanel admin tenant is not seeded yet.");
        }

        requestMetadata.TenantId = tenant.Id;

        var roleType = await dataContext.Query<IdentityRoleType>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenant.Id)
            .Where(x => x.SystemReferenceId == IdentityConstants.RoleType.Admin)
            .FirstOrDefaultAsync(ct);

        if (roleType is null)
        {
            return ControlPanelLoginResult.Failed("ControlPanel admin role is not seeded yet.");
        }

        var request = new AuthenticateIdentityRequest
        {
            UserName = username.Trim(),
            Password = password,
            RoleId = roleType.Id,
            AuthorizationType = AuthorizationType.Username,
            GenerateToken = true,
            RememberMe = rememberMe,
            Metadata = BuildMetadata(httpContext, tenant.Id)
        };

        var response = await identityServer.AuthenticateIdentity(request);
        if (!response.IsSuccess || response.Response?.Credential is null || response.Response.Identity is null)
        {
            logger.LogWarning(
                "ControlPanel login failed for {UserName}. Status={StatusCode}; Message={Message}",
                username,
                (int)response.HttpStatusCode,
                response.Message);

            var message = response.HttpStatusCode switch
            {
                HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.Forbidden =>
                    "Invalid username, password, or admin permission.",
                _ => "Unable to sign in. Check IdentityServer health and try again."
            };

            return ControlPanelLoginResult.Failed(message);
        }

        var principal = BuildPrincipal(response.Response, roleType.Id, tenant.Id, username.Trim());
        return ControlPanelLoginResult.Success(principal);
    }

    private static RequestMetadata BuildMetadata(HttpContext httpContext, Guid tenantId) => new()
    {
        TenantId = tenantId,
        RequestId = Guid.NewGuid(),
        Name = "ControlPanel",
        DeviceName = Environment.MachineName,
        DeviceAgent = httpContext.Request.Headers.UserAgent.ToString(),
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
            new(ControlPanelAuthClaims.DisplayName, displayName),
            new(ControlPanelAuthClaims.IdentityId, identityId.ToString()),
            new(ControlPanelAuthClaims.CredentialId, credentialId.ToString()),
            new(ControlPanelAuthClaims.TenantId, tenantId.ToString()),
            new(ControlPanelAuthClaims.RoleTypeId, roleTypeId.ToString()),
            new(ControlPanelAuthClaims.IsSuperUser, IsSuperUserRole(roleTypeId).ToString())
        };

        if (response.SessionId is { } sessionId)
        {
            claims.Add(new Claim(ControlPanelAuthClaims.SessionId, sessionId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, ControlPanelAuthDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    private static bool IsSuperUserRole(Guid roleTypeId) =>
        roleTypeId == ControlPanelBootstrapConstants.AdminRoleTypeId;
}

public sealed record ControlPanelLoginResult(bool IsSuccess, string? Error, ClaimsPrincipal? Principal)
{
    public static ControlPanelLoginResult Success(ClaimsPrincipal principal) => new(true, null, principal);
    public static ControlPanelLoginResult Failed(string error) => new(false, error, null);
}

public static class ControlPanelAuthDefaults
{
    public const string AuthenticationScheme = "ControlPanelCookie";
}
