using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.Patterns;

namespace XFramework.Core.Services.FeatureGates;

public sealed class TenantCredentialCapabilityService(
    DbContext dbContext,
    ILogger<TenantCredentialCapabilityService> logger) : ITenantCredentialCapabilityService
{
    public async Task<Result<bool>> IsAllowedAsync(
        Guid tenantId,
        Guid credentialId,
        string moduleKey,
        string? subFeatureKey,
        string capabilityKey,
        CancellationToken ct = default)
    {
        var result = await ResolveCapabilityAsync(
            tenantId,
            credentialId,
            moduleKey,
            subFeatureKey,
            capabilityKey,
            ct);

        if (!result.IsSuccess || result.Data is null)
            return Result<bool>.Failure(result.Message ?? "Capability check failed.", result.StatusCode);

        return Result<bool>.Success(result.Data.IsAllowed);
    }

    public async Task<Result> EnsureAllowedAsync(
        Guid tenantId,
        Guid credentialId,
        string moduleKey,
        string? subFeatureKey,
        string capabilityKey,
        CancellationToken ct = default)
    {
        var result = await ResolveCapabilityAsync(
            tenantId,
            credentialId,
            moduleKey,
            subFeatureKey,
            capabilityKey,
            ct);

        if (!result.IsSuccess)
            return Result.Failure(result.Message ?? "Capability check failed.", result.StatusCode);

        var decision = result.Data;
        if (decision is null)
            return Result.Failure("Capability check failed.", 500);

        if (decision.IsAllowed)
            return Result.Success();

        var featureKey = TenantModuleFeatureKeys.Combine(moduleKey, subFeatureKey);
        logger.LogWarning(
            "Credential capability denied for tenant {TenantId}, credential {CredentialId}, feature {FeatureKey}, capability {CapabilityKey}, source {Source}.",
            tenantId,
            credentialId,
            featureKey,
            capabilityKey,
            decision.Source);

        return Result.Forbidden($"Capability denied: '{capabilityKey}' is not allowed for feature '{featureKey}'.");
    }

    private async Task<Result<CapabilityDecision>> ResolveCapabilityAsync(
        Guid tenantId,
        Guid credentialId,
        string moduleKey,
        string? subFeatureKey,
        string capabilityKey,
        CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
            return Result<CapabilityDecision>.Failure("TenantId is required.", 400);

        if (credentialId == Guid.Empty)
            return Result<CapabilityDecision>.Failure("CredentialId is required.", 400);

        var (normalizedModuleKey, normalizedSubFeatureKey) =
            TenantModuleFeatureKeys.Normalize(moduleKey, subFeatureKey);
        var normalizedCapabilityKey = NormalizeCapability(capabilityKey);

        if (string.IsNullOrWhiteSpace(normalizedModuleKey))
            return Result<CapabilityDecision>.Failure("Module key is required.", 400);

        if (!IdentityAuthorizationConstants.CapabilityKeys.Contains(normalizedCapabilityKey))
            return Result<CapabilityDecision>.Failure("Capability key is invalid.", 400);

        var credentialExists = await dbContext.Set<IdentityCredential>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == credentialId &&
                x.TenantId == tenantId &&
                !x.IsDeleted &&
                x.IsEnabled,
                ct);

        if (!credentialExists)
            return Result<CapabilityDecision>.NotFound("Credential not found");

        var now = DateTime.UtcNow;
        var roles = await dbContext.Set<IdentityRole>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.CredentialId == credentialId &&
                x.TypeId != null &&
                !x.IsDeleted &&
                x.IsEnabled &&
                x.RoleExpiration >= now)
            .Select(x => new ActiveRole(x.Id, x.TypeId!.Value))
            .ToListAsync(ct);

        if (roles.Count == 0)
            return Result<CapabilityDecision>.Success(new CapabilityDecision(false, "NoActiveRole"));

        var roleIds = roles.Select(x => x.RoleId).ToList();
        var roleTypeIds = roles.Select(x => x.RoleTypeId).Distinct().ToList();
        var capabilityKeys = BuildCapabilityKeySet(normalizedCapabilityKey);

        var overrides = await dbContext.Set<IdentityRoleFeaturePermissionOverride>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                roleIds.Contains(x.IdentityRoleId) &&
                x.ModuleKey == normalizedModuleKey &&
                x.SubFeatureKey == normalizedSubFeatureKey &&
                capabilityKeys.Contains(x.CapabilityKey) &&
                !x.IsDeleted &&
                x.IsEnabled)
            .ToListAsync(ct);

        var roleTypePermissions = await dbContext.Set<IdentityRoleTypeFeaturePermission>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                roleTypeIds.Contains(x.RoleTypeId) &&
                x.ModuleKey == normalizedModuleKey &&
                x.SubFeatureKey == normalizedSubFeatureKey &&
                capabilityKeys.Contains(x.CapabilityKey) &&
                !x.IsDeleted &&
                x.IsEnabled)
            .ToListAsync(ct);

        var decisions = new List<CapabilityDecision>();
        foreach (var role in roles)
        {
            var roleOverrides = overrides
                .Where(x => x.IdentityRoleId == role.RoleId)
                .OrderBy(x => x.CapabilityKey == IdentityAuthorizationConstants.Manage ? 1 : 0)
                .ToList();

            if (roleOverrides.Count > 0)
            {
                decisions.AddRange(roleOverrides.Select(permission =>
                    ToDecision(permission.Effect, permission.CapabilityKey, "CredentialRoleOverride")));
                continue;
            }

            var permissions = roleTypePermissions
                .Where(x => x.RoleTypeId == role.RoleTypeId)
                .OrderBy(x => x.CapabilityKey == IdentityAuthorizationConstants.Manage ? 1 : 0)
                .ToList();

            if (permissions.Count > 0)
            {
                decisions.AddRange(permissions.Select(permission =>
                    ToDecision(permission.Effect, permission.CapabilityKey, "RoleTypePermission")));
            }
        }

        if (decisions.Any(x => !x.IsAllowed))
            return Result<CapabilityDecision>.Success(decisions.First(x => !x.IsAllowed));

        if (decisions.Any(x => x.IsAllowed))
            return Result<CapabilityDecision>.Success(decisions.First(x => x.IsAllowed));

        var policy = await dbContext.Set<TenantAuthorizationPolicy>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                !x.IsDeleted &&
                x.IsEnabled)
            .Select(x => (MissingPermissionBehavior?)x.MissingPermissionBehavior)
            .FirstOrDefaultAsync(ct) ?? MissingPermissionBehavior.Deny;

        return Result<CapabilityDecision>.Success(policy == MissingPermissionBehavior.Allow
            ? new CapabilityDecision(true, "TenantDefaultAllow")
            : new CapabilityDecision(false, "TenantDefaultDeny"));
    }

    private static CapabilityDecision ToDecision(
        RoleCapabilityPermissionEffect effect,
        string matchedCapability,
        string source) =>
        effect == RoleCapabilityPermissionEffect.Allow
            ? new CapabilityDecision(true, $"{source}:Allow:{matchedCapability}")
            : new CapabilityDecision(false, $"{source}:Deny:{matchedCapability}");

    private static HashSet<string> BuildCapabilityKeySet(string capabilityKey)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            capabilityKey
        };

        if (!string.Equals(capabilityKey, IdentityAuthorizationConstants.Manage, StringComparison.OrdinalIgnoreCase))
        {
            keys.Add(IdentityAuthorizationConstants.Manage);
        }

        return keys;
    }

    private static string NormalizeCapability(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    private sealed record ActiveRole(Guid RoleId, Guid RoleTypeId);

    private sealed record CapabilityDecision(bool IsAllowed, string Source);
}
