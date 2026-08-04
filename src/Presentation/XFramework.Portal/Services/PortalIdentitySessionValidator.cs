using System.Security.Claims;
using XFramework.Integration.Security;

namespace XFramework.Portal.Services;

public sealed class PortalIdentitySessionValidator(
    IActorIdentityProvider actorIdentityProvider,
    ILogger<PortalIdentitySessionValidator> logger)
{
    public static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(5);

    public async Task<bool> ValidateAsync(ClaimsPrincipal? principal, CancellationToken ct = default)
    {
        if (!TryReadSessionClaims(principal, out var tenantId, out var credentialId, out var sessionId, out _) ||
            string.IsNullOrWhiteSpace(principal?.FindFirst(PortalAuthClaims.ActorAccessToken)?.Value))
        {
            logger.LogWarning("Portal principal is missing required IdentityServer actor claims.");
            return false;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ValidationTimeout);
            var validation = await actorIdentityProvider.ValidateAsync(
                principal.FindFirst(PortalAuthClaims.ActorAccessToken)!.Value,
                timeout.Token);
            var actor = validation.Identity;

            return validation.IsValid &&
                actor is not null &&
                actor.TenantId == tenantId &&
                actor.CredentialId == credentialId &&
                actor.SessionId == sessionId;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("Portal session validation was canceled.");
            return false;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Portal session validation timed out after {Timeout}.", ValidationTimeout);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Portal session validation failed because IdentityServer was unavailable.");
            return false;
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

    private static bool TryReadGuidClaim(ClaimsPrincipal principal, string claimType, out Guid value) =>
        Guid.TryParse(principal.FindFirst(claimType)?.Value, out value) && value != Guid.Empty;
}
