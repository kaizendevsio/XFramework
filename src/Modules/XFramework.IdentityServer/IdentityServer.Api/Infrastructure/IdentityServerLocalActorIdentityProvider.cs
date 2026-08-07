using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Security;
using XFramework.Integration.Services;
using Session = IdentityServer.Domain.Shared.Contracts.Session;

namespace IdentityServer.Api.Infrastructure;

public sealed class IdentityServerLocalActorIdentityProvider(
    IJwtService jwtService,
    IDataContext dataContext,
    IIdentityAuthorizationService authorizationService,
    TimeProvider timeProvider,
    ILogger<IdentityServerLocalActorIdentityProvider> logger)
    : IActorIdentityProvider
{
    private string? _cachedToken;
    private ActorIdentityValidationResult? _cachedResult;

    public async Task<ActorIdentityValidationResult> ValidateAsync(
        string token,
        CancellationToken ct = default)
    {
        if (string.Equals(token, _cachedToken, StringComparison.Ordinal) && _cachedResult is not null)
            return _cachedResult;

        try
        {
            var (principal, jwt) = await jwtService.DecodeJwtToken(token);
            if (!TryReadGuid(principal, "tenant_id", out var tenantId) ||
                !TryReadGuid(principal, "credential_id", out var credentialId) ||
                !TryReadGuid(principal, "session_id", out var sessionId))
            {
                return Cache(token, ActorIdentityValidationResult.Failure("Actor token claims are incomplete."));
            }

            var generationId = principal.FindFirst(JwtCredentialSet.GenerationClaim)?.Value;
            if (string.IsNullOrWhiteSpace(generationId))
                return Cache(token, ActorIdentityValidationResult.Failure("Actor token generation is missing."));

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var sessionIsActive = await dataContext.Query<Session>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(session => session.Id == sessionId)
                .Where(session => session.TenantId == tenantId)
                .Where(session => session.CredentialId == credentialId)
                .Where(session => session.Status == CurrentSessionState.Active)
                .Where(session => !session.IsDeleted && session.IsEnabled)
                .Where(session => session.ExpiresAt == null || session.ExpiresAt > now)
                .AnyAsync(ct);
            if (!sessionIsActive)
                return Cache(token, ActorIdentityValidationResult.Failure("Identity session is no longer valid."));

            var tenantIsActive = await dataContext.Query<Tenant>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(tenant => tenant.Id == tenantId)
                .Where(tenant => !tenant.IsDeleted && tenant.IsEnabled)
                .Where(tenant => tenant.AvailabilityDate == null || tenant.AvailabilityDate <= now)
                .Where(tenant => tenant.Expiration == null || tenant.Expiration > now)
                .AnyAsync(ct);
            if (!tenantIsActive)
                return Cache(token, ActorIdentityValidationResult.Failure("Identity session is no longer valid."));

            var credential = await dataContext.Query<IdentityCredential>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(item => item.Id == credentialId && item.TenantId == tenantId)
                .Where(item => !item.IsDeleted && item.IsEnabled)
                .FirstOrDefaultAsync(ct);
            if (credential is null)
                return Cache(token, ActorIdentityValidationResult.Failure("Identity session is no longer valid."));

            var identity = await dataContext.Query<IdentityInformation>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(identity => identity.Id == credential.IdentityInfoId && identity.TenantId == tenantId)
                .Where(identity => !identity.IsDeleted && identity.IsEnabled)
                .FirstOrDefaultAsync(ct);
            if (identity is null)
                return Cache(token, ActorIdentityValidationResult.Failure("Identity session is no longer valid."));

            var activeRoles = await dataContext.Query<IdentityRole>()
                .IgnoreQueryFilters()
                .NoCache()
                .Include(role => role.Type)
                .Where(role => role.TenantId == tenantId && role.CredentialId == credentialId)
                .Where(role => !role.IsDeleted && role.IsEnabled)
                .Where(role => role.TypeId != null && role.RoleExpiration >= now)
                .Where(role => role.Type != null && !role.Type.IsDeleted && role.Type.IsEnabled)
                .Take(101)
                .ToListAsync(ct);
            if (activeRoles.Count > 100)
                return Cache(token, ActorIdentityValidationResult.Failure("Actor authorization state exceeds supported limits.", 409));

            var claimedRoleIds = ReadRoleTypeIds(principal);
            var activeRoleIds = activeRoles.Select(role => role.TypeId!.Value).ToHashSet();
            if (!claimedRoleIds.SetEquals(activeRoleIds))
                return Cache(token, ActorIdentityValidationResult.Failure("Identity roles have changed. Sign in again."));

            var capabilityResult = await authorizationService.GetTrustedEffectiveCredentialCapabilitiesAsync(
                tenantId,
                credentialId,
                ct);
            if (!capabilityResult.IsSuccess || capabilityResult.Data is null)
            {
                return Cache(token, ActorIdentityValidationResult.Failure(
                    capabilityResult.Message ?? "Actor capabilities could not be resolved.",
                    capabilityResult.StatusCode));
            }

            var roles = activeRoles
                .SelectMany(role => new[] { role.TypeId!.Value.ToString("D"), role.Type!.Name })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var capabilities = capabilityResult.Data.Capabilities
                .Where(capability => capability.IsAllowed)
                .Select(capability =>
                    $"{TenantModuleFeatureKeys.Combine(capability.ModuleKey, capability.SubFeatureKey)}:{capability.CapabilityKey}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [IdentityAuthorizationConstants.ActorAttributeIdentityVerified] =
                    identity.IsVerified ? bool.TrueString : bool.FalseString
            };

            return Cache(token, ActorIdentityValidationResult.Success(new TrustedActorIdentity(
                credentialId,
                credential.IdentityInfoId,
                tenantId,
                sessionId,
                roles,
                capabilities,
                generationId,
                new DateTimeOffset(DateTime.SpecifyKind(jwt.ValidTo, DateTimeKind.Utc)),
                attributes)));
        }
        catch (SecurityTokenException)
        {
            return Cache(token, ActorIdentityValidationResult.Failure("Actor token is invalid."));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Actor identity validation failed closed.");
            return Cache(token, ActorIdentityValidationResult.Failure("Actor identity validation is unavailable.", 503));
        }
    }

    private ActorIdentityValidationResult Cache(string token, ActorIdentityValidationResult result)
    {
        _cachedToken = token;
        _cachedResult = result;
        return result;
    }

    private static bool TryReadGuid(ClaimsPrincipal principal, string claimType, out Guid value) =>
        Guid.TryParse(principal.FindFirst(claimType)?.Value, out value) && value != Guid.Empty;

    private static HashSet<Guid> ReadRoleTypeIds(ClaimsPrincipal principal)
    {
        var result = new HashSet<Guid>();
        foreach (var claim in principal.FindAll(ClaimTypes.Role))
        {
            if (Guid.TryParse(claim.Value, out var roleTypeId))
            {
                result.Add(roleTypeId);
                continue;
            }

            try
            {
                foreach (var value in System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(claim.Value) ?? [])
                    result.Add(value);
            }
            catch (System.Text.Json.JsonException)
            {
                result.Add(Guid.Empty);
            }
        }

        return result;
    }
}
