using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using XFramework.Integration.Security;
using XFramework.Portal.Shared;

namespace XFramework.Portal.Services;

public sealed class PortalActorAccessTokenProvider : IActorAccessTokenProvider
{
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(1);
    private static readonly JwtSecurityTokenHandler TokenHandler = new();
    private readonly PortalActorContext _actorContext;
    private readonly PortalActorAccessTokenScope _actorAccessTokenScope;
    private readonly Func<ClaimsPrincipal?, CancellationToken, Task<PortalSessionValidationResult>> _validateAndRefresh;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public PortalActorAccessTokenProvider(
        PortalActorContext actorContext,
        PortalActorAccessTokenScope actorAccessTokenScope,
        Func<PortalIdentitySessionValidator> getSessionValidator,
        TimeProvider timeProvider)
        : this(
            actorContext,
            actorAccessTokenScope,
            (principal, ct) => getSessionValidator().ValidateAndRefreshAsync(principal, ct),
            timeProvider)
    {
    }

    internal PortalActorAccessTokenProvider(
        PortalActorContext actorContext,
        PortalActorAccessTokenScope actorAccessTokenScope,
        Func<ClaimsPrincipal?, CancellationToken, Task<PortalSessionValidationResult>> validateAndRefresh,
        TimeProvider timeProvider)
    {
        _actorContext = actorContext;
        _actorAccessTokenScope = actorAccessTokenScope;
        _validateAndRefresh = validateAndRefresh;
        _timeProvider = timeProvider;
    }

    public async ValueTask<string?> GetTokenAsync(CancellationToken ct = default)
    {
        if (_actorAccessTokenScope.TryGetToken(out var scopedToken))
            return scopedToken;

        var principal = await _actorContext.GetAuthenticatedPrincipalAsync(ct);
        var accessToken = principal?.FindFirst(PortalAuthClaims.ActorAccessToken)?.Value;
        if (string.IsNullOrWhiteSpace(accessToken) || HasUsableLifetime(accessToken))
            return accessToken;

        await _refreshGate.WaitAsync(ct);
        try
        {
            principal = await _actorContext.GetAuthenticatedPrincipalAsync(ct);
            accessToken = principal?.FindFirst(PortalAuthClaims.ActorAccessToken)?.Value;
            if (string.IsNullOrWhiteSpace(accessToken) || HasUsableLifetime(accessToken))
                return accessToken;

            var validation = await _validateAndRefresh(principal, ct);
            return validation.IsValid
                ? principal?.FindFirst(PortalAuthClaims.ActorAccessToken)?.Value
                : null;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private bool HasUsableLifetime(string token)
    {
        try
        {
            var expiresAt = TokenHandler.ReadJwtToken(token).ValidTo;
            return expiresAt > _timeProvider.GetUtcNow().UtcDateTime.Add(RefreshWindow);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
