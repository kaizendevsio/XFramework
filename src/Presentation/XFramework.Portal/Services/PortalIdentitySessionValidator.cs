using System.Net;
using System.Security.Claims;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace XFramework.Portal.Services;

public sealed class PortalIdentitySessionValidator(
    IActorIdentityProvider actorIdentityProvider,
    IIdentityServerServiceWrapper identityServer,
    PortalActorAccessTokenProvider actorAccessTokenProvider,
    PortalActorTokenRefreshCoordinator refreshCoordinator,
    ILogger<PortalIdentitySessionValidator> logger)
{
    public static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(5);

    public async Task<bool> ValidateAsync(ClaimsPrincipal? principal, CancellationToken ct = default) =>
        (await ValidateAndRefreshAsync(principal, ct)).IsValid;

    public async Task<PortalSessionValidationResult> ValidateAndRefreshAsync(
        ClaimsPrincipal? principal,
        CancellationToken ct = default)
    {
        if (!TryReadSessionClaims(principal, out var tenantId, out var credentialId, out var sessionId, out _) ||
            principal?.Identity is not ClaimsIdentity identity ||
            string.IsNullOrWhiteSpace(principal.FindFirst(PortalAuthClaims.ActorAccessToken)?.Value))
        {
            logger.LogWarning("Portal principal is missing required IdentityServer actor claims.");
            return PortalSessionValidationResult.Invalid;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ValidationTimeout);
            var accessToken = principal.FindFirst(PortalAuthClaims.ActorAccessToken)!.Value;
            var validation = await actorIdentityProvider.ValidateAsync(accessToken, timeout.Token);
            if (HasExpectedBindings(validation, tenantId, credentialId, sessionId))
                return PortalSessionValidationResult.Valid;

            if (validation.StatusCode != (int)HttpStatusCode.Unauthorized)
                return PortalSessionValidationResult.Invalid;

            var refreshToken = principal.FindFirst(PortalAuthClaims.RefreshToken)?.Value;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                logger.LogInformation(
                    "Portal session {SessionId} requires sign-in because it has no refresh credential.",
                    sessionId);
                return PortalSessionValidationResult.Invalid;
            }

            var refreshedTokens = await refreshCoordinator.RefreshAsync(
                sessionId,
                refreshToken,
                refreshCt => RefreshTokenAsync(accessToken, refreshToken, sessionId, refreshCt),
                timeout.Token);
            if (refreshedTokens is null)
                return PortalSessionValidationResult.Invalid;

            var refreshedValidation = await actorIdentityProvider.ValidateAsync(
                refreshedTokens.AccessToken,
                timeout.Token);
            if (!HasExpectedBindings(refreshedValidation, tenantId, credentialId, sessionId))
            {
                refreshCoordinator.Remove(sessionId);
                return PortalSessionValidationResult.Invalid;
            }

            ReplaceClaim(identity, PortalAuthClaims.ActorAccessToken, refreshedTokens.AccessToken);
            ReplaceClaim(identity, PortalAuthClaims.RefreshToken, refreshedTokens.RefreshToken);
            logger.LogInformation("Refreshed the actor token for Portal session {SessionId}.", sessionId);
            return PortalSessionValidationResult.Refreshed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("Portal session validation was canceled.");
            return PortalSessionValidationResult.Invalid;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Portal session validation timed out after {Timeout}.", ValidationTimeout);
            return PortalSessionValidationResult.Invalid;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Portal session validation failed because IdentityServer was unavailable.");
            return PortalSessionValidationResult.Invalid;
        }
    }

    public static bool TryReadSessionClaims(
        ClaimsPrincipal? principal,
        out Guid tenantId,
        out Guid credentialId,
        out Guid sessionId,
        out Guid roleTypeId)
    {
        tenantId = default;
        credentialId = default;
        sessionId = default;
        roleTypeId = default;

        return principal?.Identity?.IsAuthenticated == true
            && TryReadGuidClaim(principal, PortalAuthClaims.TenantId, out tenantId)
            && TryReadGuidClaim(principal, PortalAuthClaims.CredentialId, out credentialId)
            && TryReadGuidClaim(principal, PortalAuthClaims.SessionId, out sessionId)
            && TryReadGuidClaim(principal, PortalAuthClaims.RoleTypeId, out roleTypeId);
    }

    private async Task<PortalActorTokenPair?> RefreshTokenAsync(
        string accessToken,
        string refreshToken,
        Guid sessionId,
        CancellationToken ct)
    {
        using var suppressedActor = actorAccessTokenProvider.Suppress();
        var response = await identityServer.RefreshToken(
            new RefreshTokenRequest
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                SessionId = sessionId,
                Metadata = new RequestMetadata
                {
                    RequestId = Guid.NewGuid(),
                    OperationName = "Refresh Portal actor token",
                    DeviceName = Environment.MachineName
                }
            },
            ct);

        if (!response.IsSuccess ||
            response.Response is not { } tokens ||
            string.IsNullOrWhiteSpace(tokens.AccessToken) ||
            string.IsNullOrWhiteSpace(tokens.RefreshToken) ||
            tokens.SessionId != sessionId)
        {
            logger.LogInformation(
                "IdentityServer did not refresh Portal session {SessionId}. Status={StatusCode}.",
                sessionId,
                (int)response.HttpStatusCode);
            return null;
        }

        return new PortalActorTokenPair(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.SessionId,
            tokens.ExpiresIn);
    }

    private static bool HasExpectedBindings(
        ActorIdentityValidationResult validation,
        Guid tenantId,
        Guid credentialId,
        Guid sessionId) =>
        validation.IsValid &&
        validation.Identity is { } actor &&
        actor.TenantId == tenantId &&
        actor.CredentialId == credentialId &&
        actor.SessionId == sessionId;

    private static void ReplaceClaim(ClaimsIdentity identity, string claimType, string value)
    {
        foreach (var existing in identity.FindAll(claimType).ToArray())
            identity.RemoveClaim(existing);

        identity.AddClaim(new Claim(claimType, value));
    }

    private static bool TryReadGuidClaim(ClaimsPrincipal principal, string claimType, out Guid value) =>
        Guid.TryParse(principal.FindFirst(claimType)?.Value, out value) && value != Guid.Empty;
}

public sealed record PortalSessionValidationResult(bool IsValid, bool WasRefreshed)
{
    public static PortalSessionValidationResult Valid { get; } = new(true, false);
    public static PortalSessionValidationResult Refreshed { get; } = new(true, true);
    public static PortalSessionValidationResult Invalid { get; } = new(false, false);
}
