using System.Security.Claims;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Portal.Services;

public sealed class PortalIdentitySessionValidator(
    IIdentityServerServiceWrapper identityServer,
    ILogger<PortalIdentitySessionValidator> logger)
{
    public static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(5);

    public async Task<bool> ValidateAsync(ClaimsPrincipal? principal, CancellationToken ct = default)
    {
        if (!TryReadSessionClaims(principal, out var tenantId, out var credentialId, out var sessionId, out var roleTypeId))
        {
            logger.LogWarning("Portal principal is missing required IdentityServer session claims.");
            return false;
        }

        var request = new ValidateIdentitySessionRequest
        {
            TenantId = tenantId,
            CredentialId = credentialId,
            SessionId = sessionId,
            RoleTypeIds = [roleTypeId],
            Metadata = new RequestMetadata
            {
                TenantId = tenantId,
                CredentialId = credentialId,
                SessionId = sessionId,
                RequestId = Guid.NewGuid(),
                Name = "Portal session validation",
                DeviceName = Environment.MachineName
            }
        };

        try
        {
            var result = await identityServer.ValidateIdentitySession(request)
                .WaitAsync(ValidationTimeout, ct);
            var response = result.Response;

            return result.IsSuccess
                && response is
                {
                    IsValid: true
                }
                && response.TenantId == tenantId
                && response.CredentialId == credentialId
                && response.SessionId == sessionId;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("Portal session validation was canceled.");
            return false;
        }
        catch (TimeoutException ex)
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
        Guid.TryParse(principal.FindFirst(claimType)?.Value, out value)
        && value != Guid.Empty;
}
