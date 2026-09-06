using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using XFramework.Portal.Shared;

namespace XFramework.Portal.Services;

public sealed class PortalActorContext(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authenticationStateProvider) : IPortalActorContext
{
    public Guid? CredentialId => ReadGuidClaim(GetAvailablePrincipal(), PortalAuthClaims.CredentialId);
    public Guid? SessionId => ReadGuidClaim(GetAvailablePrincipal(), PortalAuthClaims.SessionId);

    public async ValueTask<ClaimsPrincipal?> GetAuthenticatedPrincipalAsync(CancellationToken ct = default)
    {
        try
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync().WaitAsync(ct);
            if (state.User.Identity?.IsAuthenticated == true)
                return state.User;
        }
        catch (InvalidOperationException)
        {
            // Authentication state exists only inside a Blazor circuit; HTTP and background scopes use the fallback.
        }

        return GetAuthenticatedRequestPrincipal();
    }

    public async ValueTask<string?> GetActorAccessTokenAsync(CancellationToken ct = default) =>
        (await GetAuthenticatedPrincipalAsync(ct))?.FindFirst(PortalAuthClaims.ActorAccessToken)?.Value;

    private ClaimsPrincipal? GetAvailablePrincipal()
    {
        try
        {
            var state = authenticationStateProvider.GetAuthenticationStateAsync();
            if (state.IsCompletedSuccessfully && state.Result.User.Identity?.IsAuthenticated == true)
                return state.Result.User;
        }
        catch (InvalidOperationException)
        {
            // Authentication state exists only inside a Blazor circuit; HTTP and background scopes use the fallback.
        }

        return GetAuthenticatedRequestPrincipal();
    }

    private ClaimsPrincipal? GetAuthenticatedRequestPrincipal()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true ? principal : null;
    }

    private static Guid? ReadGuidClaim(ClaimsPrincipal? principal, string claimType) =>
        Guid.TryParse(principal?.FindFirst(claimType)?.Value, out var value) && value != Guid.Empty
            ? value
            : null;
}
