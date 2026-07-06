using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using System.Security.Claims;

namespace IdentityServer.Api.Services;

public sealed class IdentityAuthorizationService(
    IDataContext dataContext,
    ITrustedServiceInvocationResolver trustedServiceInvocationResolver,
    IHttpContextAccessor httpContextAccessor,
    ILogger<IdentityAuthorizationService> logger) : IIdentityAuthorizationService
{
    private const string SourceCredentialOverrideAllow = "CredentialRoleOverrideAllow";
    private const string SourceCredentialOverrideDeny = "CredentialRoleOverrideDeny";
    private const string SourceRoleTypeAllow = "RoleTypePermissionAllow";
    private const string SourceRoleTypeDeny = "RoleTypePermissionDeny";
    private const string SourceTenantDefaultAllow = "TenantDefaultAllow";
    private const string SourceTenantDefaultDeny = "TenantDefaultDeny";
    private const string SourceTenantFeatureDisabled = "TenantFeatureDisabled";

    public async Task<Result<CredentialCapabilityCheckResponse>> CheckCredentialCapabilityAsync(
        CheckCredentialCapabilityRequest request,
        CancellationToken ct = default)
    {
        var normalized = NormalizePermissionTarget(request.ModuleKey, request.SubFeatureKey, request.CapabilityKey);
        if (!normalized.IsSuccess)
            return Result<CredentialCapabilityCheckResponse>.Failure(normalized.Message!, normalized.StatusCode);

        var credential = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == request.CredentialId)
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (credential is null)
            return Result<CredentialCapabilityCheckResponse>.NotFound("Credential not found");

        if (request.Metadata.TenantId is { } metadataTenantId && metadataTenantId != credential.TenantId)
            return Result<CredentialCapabilityCheckResponse>.Failure("Credential does not belong to the active tenant", 403);

        var inspectAuthorization = await EnsureCanInspectCredentialCapabilitiesAsync(
            request.Metadata,
            credential.TenantId,
            credential.Id,
            ct);
        if (!inspectAuthorization.IsSuccess)
            return Result<CredentialCapabilityCheckResponse>.Failure(
                inspectAuthorization.Message!,
                inspectAuthorization.StatusCode);

        var decision = await ResolveCapabilityAsync(
            credential.TenantId,
            credential.Id,
            normalized.Data!.ModuleKey,
            normalized.Data.SubFeatureKey,
            normalized.Data.CapabilityKey,
            ct,
            credentialAlreadyValidated: true);

        return Result<CredentialCapabilityCheckResponse>.Success(new CredentialCapabilityCheckResponse
        {
            TenantId = credential.TenantId,
            CredentialId = credential.Id,
            ModuleKey = normalized.Data.ModuleKey,
            SubFeatureKey = normalized.Data.SubFeatureKey,
            CapabilityKey = normalized.Data.CapabilityKey,
            IsAllowed = decision.IsAllowed,
            DecisionSource = decision.Source
        });
    }

    public async Task<Result<EffectiveCredentialCapabilitiesResponse>> GetEffectiveCredentialCapabilitiesAsync(
        GetEffectiveCredentialCapabilitiesRequest request,
        CancellationToken ct = default)
    {
        var credential = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == request.CredentialId)
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (credential is null)
            return Result<EffectiveCredentialCapabilitiesResponse>.NotFound("Credential not found");

        if (request.Metadata.TenantId is { } metadataTenantId && metadataTenantId != credential.TenantId)
            return Result<EffectiveCredentialCapabilitiesResponse>.Failure(
                "Credential does not belong to the active tenant",
                403);

        var inspectAuthorization = await EnsureCanInspectCredentialCapabilitiesAsync(
            request.Metadata,
            credential.TenantId,
            credential.Id,
            ct);
        if (!inspectAuthorization.IsSuccess)
            return Result<EffectiveCredentialCapabilitiesResponse>.Failure(
                inspectAuthorization.Message!,
                inspectAuthorization.StatusCode);

        var response = new EffectiveCredentialCapabilitiesResponse
        {
            TenantId = credential.TenantId,
            CredentialId = credential.Id
        };

        foreach (var feature in TenantModuleFeatureKeys.All.OrderBy(x => x.ModuleKey).ThenBy(x => x.SubFeatureKey))
        {
            foreach (var capability in IdentityAuthorizationConstants.CapabilityKeys)
            {
                var decision = await ResolveCapabilityAsync(
                    credential.TenantId,
                    credential.Id,
                    feature.ModuleKey,
                    feature.SubFeatureKey,
                    capability,
                    ct,
                    credentialAlreadyValidated: true);

                response.Capabilities.Add(new CredentialCapabilityCheckResponse
                {
                    TenantId = credential.TenantId,
                    CredentialId = credential.Id,
                    ModuleKey = feature.ModuleKey,
                    SubFeatureKey = feature.SubFeatureKey,
                    CapabilityKey = capability,
                    IsAllowed = decision.IsAllowed,
                    DecisionSource = decision.Source
                });
            }
        }

        return Result<EffectiveCredentialCapabilitiesResponse>.Success(response);
    }

    public async Task<Result<TenantAuthorizationPolicyResponse>> GetTenantAuthorizationPolicyAsync(
        GetTenantAuthorizationPolicyRequest request,
        CancellationToken ct = default)
    {
        var tenantId = ResolveRequestedTenantId(request.TenantId, request.Metadata.TenantId);
        if (!tenantId.IsSuccess)
            return Result<TenantAuthorizationPolicyResponse>.Failure(tenantId.Message!, tenantId.StatusCode);
        var resolvedTenantId = tenantId.Data;

        var tenantExists = await TenantExistsAsync(resolvedTenantId, ct);
        if (!tenantExists)
            return Result<TenantAuthorizationPolicyResponse>.NotFound("Tenant not found");

        var authorization = await EnsureCallerCapabilityAsync(
            request.Metadata,
            resolvedTenantId,
            TenantModuleFeatureKeys.Identity,
            TenantModuleFeatureKeys.TenantsSubFeature,
            IdentityAuthorizationConstants.View,
            ct);
        if (!authorization.IsSuccess)
            return Result<TenantAuthorizationPolicyResponse>.Failure(authorization.Message!, authorization.StatusCode);

        var policy = await dataContext.Query<TenantAuthorizationPolicy>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == resolvedTenantId)
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);

        return Result<TenantAuthorizationPolicyResponse>.Success(new TenantAuthorizationPolicyResponse
        {
            TenantId = resolvedTenantId,
            MissingPermissionBehavior = policy?.MissingPermissionBehavior ?? MissingPermissionBehavior.Deny
        });
    }

    public async Task<Result<TenantAuthorizationPolicyResponse>> UpdateTenantAuthorizationPolicyAsync(
        UpdateTenantAuthorizationPolicyRequest request,
        CancellationToken ct = default)
    {
        var tenantId = ResolveRequestedTenantId(request.TenantId, request.Metadata.TenantId);
        if (!tenantId.IsSuccess)
            return Result<TenantAuthorizationPolicyResponse>.Failure(tenantId.Message!, tenantId.StatusCode);
        var resolvedTenantId = tenantId.Data;

        var tenantExists = await TenantExistsAsync(resolvedTenantId, ct);
        if (!tenantExists)
            return Result<TenantAuthorizationPolicyResponse>.NotFound("Tenant not found");

        var authorization = await EnsureCallerCapabilityAsync(
            request.Metadata,
            resolvedTenantId,
            TenantModuleFeatureKeys.Identity,
            TenantModuleFeatureKeys.TenantsSubFeature,
            IdentityAuthorizationConstants.Manage,
            ct);
        if (!authorization.IsSuccess)
            return Result<TenantAuthorizationPolicyResponse>.Failure(authorization.Message!, authorization.StatusCode);

        var policy = await dataContext.Query<TenantAuthorizationPolicy>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == resolvedTenantId)
            .FirstOrDefaultAsync(ct);

        if (policy is null)
        {
            policy = new TenantAuthorizationPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = resolvedTenantId,
                MissingPermissionBehavior = request.MissingPermissionBehavior,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };
            dataContext.Add(policy);
        }
        else
        {
            policy.MissingPermissionBehavior = request.MissingPermissionBehavior;
            policy.IsEnabled = true;
            policy.IsDeleted = false;
            policy.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(policy);
        }

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<TenantAuthorizationPolicyResponse>.Failure("Tenant authorization policy could not be saved", saveResult.StatusCode);

        return Result<TenantAuthorizationPolicyResponse>.Success(new TenantAuthorizationPolicyResponse
        {
            TenantId = resolvedTenantId,
            MissingPermissionBehavior = policy.MissingPermissionBehavior
        });
    }

    public async Task<Result<RoleTypePermissionsResponse>> GetRoleTypePermissionsAsync(
        GetRoleTypePermissionsRequest request,
        CancellationToken ct = default)
    {
        var roleType = await GetRoleTypeAsync(request.RoleTypeId, request.Metadata.TenantId, ct);
        if (!roleType.IsSuccess)
            return Result<RoleTypePermissionsResponse>.Failure(roleType.Message!, roleType.StatusCode);

        var authorization = await EnsureCallerCapabilityAsync(
            request.Metadata,
            roleType.Data!.TenantId,
            TenantModuleFeatureKeys.Identity,
            TenantModuleFeatureKeys.RolesSubFeature,
            IdentityAuthorizationConstants.View,
            ct);
        if (!authorization.IsSuccess)
            return Result<RoleTypePermissionsResponse>.Failure(authorization.Message!, authorization.StatusCode);

        var permissions = await dataContext.Query<IdentityRoleTypeFeaturePermission>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == roleType.Data!.TenantId)
            .Where(x => x.RoleTypeId == roleType.Data.Id)
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .OrderBy(x => x.ModuleKey)
            .ThenBy(x => x.SubFeatureKey)
            .ThenBy(x => x.CapabilityKey)
            .ToListAsync(ct);

        return Result<RoleTypePermissionsResponse>.Success(new RoleTypePermissionsResponse
        {
            TenantId = roleType.Data.TenantId,
            RoleTypeId = roleType.Data.Id,
            Permissions = permissions.Select(ToPermissionDto).ToList()
        });
    }

    public async Task<Result<RoleTypePermissionsResponse>> SetRoleTypePermissionsAsync(
        SetRoleTypePermissionsRequest request,
        CancellationToken ct = default)
    {
        var roleType = await GetRoleTypeAsync(request.RoleTypeId, request.Metadata.TenantId, ct);
        if (!roleType.IsSuccess)
            return Result<RoleTypePermissionsResponse>.Failure(roleType.Message!, roleType.StatusCode);

        var authorization = await EnsureCallerCapabilityAsync(
            request.Metadata,
            roleType.Data!.TenantId,
            TenantModuleFeatureKeys.Identity,
            TenantModuleFeatureKeys.RolesSubFeature,
            IdentityAuthorizationConstants.Update,
            ct);
        if (!authorization.IsSuccess)
            return Result<RoleTypePermissionsResponse>.Failure(authorization.Message!, authorization.StatusCode);

        var normalized = NormalizePermissionDtos(request.Permissions);
        if (!normalized.IsSuccess)
            return Result<RoleTypePermissionsResponse>.Failure(normalized.Message!, normalized.StatusCode);

        var existing = await dataContext.Query<IdentityRoleTypeFeaturePermission>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == roleType.Data!.TenantId)
            .Where(x => x.RoleTypeId == roleType.Data.Id)
            .ToListAsync(ct);

        var existingByKey = existing.ToDictionary(BuildPermissionKey, StringComparer.OrdinalIgnoreCase);
        var desiredKeys = normalized.Data!
            .Select(BuildPermissionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in normalized.Data!)
        {
            if (existingByKey.TryGetValue(BuildPermissionKey(permission), out var row))
            {
                row.Effect = permission.Effect;
                row.IsEnabled = true;
                row.IsDeleted = false;
                row.ModifiedAt = DateTime.UtcNow;
                dataContext.Update(row);
                continue;
            }

            dataContext.Add(new IdentityRoleTypeFeaturePermission
            {
                Id = Guid.NewGuid(),
                TenantId = roleType.Data.TenantId,
                RoleTypeId = roleType.Data.Id,
                ModuleKey = permission.ModuleKey,
                SubFeatureKey = permission.SubFeatureKey,
                CapabilityKey = permission.CapabilityKey,
                Effect = permission.Effect,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }

        foreach (var row in existing.Where(row => !desiredKeys.Contains(BuildPermissionKey(row))))
        {
            row.IsEnabled = false;
            row.IsDeleted = true;
            row.DeletedAt = DateTime.UtcNow;
            dataContext.Update(row);
        }

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<RoleTypePermissionsResponse>.Failure("Role type permissions could not be saved", saveResult.StatusCode);

        return await GetRoleTypePermissionsAsync(new GetRoleTypePermissionsRequest
        {
            RoleTypeId = request.RoleTypeId,
            Metadata = request.Metadata
        }, ct);
    }

    public async Task<Result<CredentialRolePermissionOverridesResponse>> GetCredentialRolePermissionOverridesAsync(
        GetCredentialRolePermissionOverridesRequest request,
        CancellationToken ct = default)
    {
        var role = await GetIdentityRoleAsync(request.IdentityRoleId, request.Metadata.TenantId, ct);
        if (!role.IsSuccess)
            return Result<CredentialRolePermissionOverridesResponse>.Failure(role.Message!, role.StatusCode);

        var authorization = await EnsureCallerCapabilityAsync(
            request.Metadata,
            role.Data!.TenantId,
            TenantModuleFeatureKeys.Identity,
            TenantModuleFeatureKeys.RolesSubFeature,
            IdentityAuthorizationConstants.View,
            ct);
        if (!authorization.IsSuccess)
            return Result<CredentialRolePermissionOverridesResponse>.Failure(
                authorization.Message!,
                authorization.StatusCode);

        var overrides = await dataContext.Query<IdentityRoleFeaturePermissionOverride>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == role.Data!.TenantId)
            .Where(x => x.IdentityRoleId == role.Data.Id)
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .OrderBy(x => x.ModuleKey)
            .ThenBy(x => x.SubFeatureKey)
            .ThenBy(x => x.CapabilityKey)
            .ToListAsync(ct);

        return Result<CredentialRolePermissionOverridesResponse>.Success(new CredentialRolePermissionOverridesResponse
        {
            TenantId = role.Data.TenantId,
            IdentityRoleId = role.Data.Id,
            Overrides = overrides.Select(ToPermissionDto).ToList()
        });
    }

    public async Task<Result<CredentialRolePermissionOverridesResponse>> SetCredentialRolePermissionOverridesAsync(
        SetCredentialRolePermissionOverridesRequest request,
        CancellationToken ct = default)
    {
        var role = await GetIdentityRoleAsync(request.IdentityRoleId, request.Metadata.TenantId, ct);
        if (!role.IsSuccess)
            return Result<CredentialRolePermissionOverridesResponse>.Failure(role.Message!, role.StatusCode);

        var authorization = await EnsureCallerCapabilityAsync(
            request.Metadata,
            role.Data!.TenantId,
            TenantModuleFeatureKeys.Identity,
            TenantModuleFeatureKeys.RolesSubFeature,
            IdentityAuthorizationConstants.Update,
            ct);
        if (!authorization.IsSuccess)
            return Result<CredentialRolePermissionOverridesResponse>.Failure(
                authorization.Message!,
                authorization.StatusCode);

        var normalized = NormalizePermissionDtos(request.Overrides);
        if (!normalized.IsSuccess)
            return Result<CredentialRolePermissionOverridesResponse>.Failure(normalized.Message!, normalized.StatusCode);

        var existing = await dataContext.Query<IdentityRoleFeaturePermissionOverride>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == role.Data!.TenantId)
            .Where(x => x.IdentityRoleId == role.Data.Id)
            .ToListAsync(ct);

        var existingByKey = existing.ToDictionary(BuildPermissionKey, StringComparer.OrdinalIgnoreCase);
        var desiredKeys = normalized.Data!
            .Select(BuildPermissionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in normalized.Data!)
        {
            if (existingByKey.TryGetValue(BuildPermissionKey(permission), out var row))
            {
                row.Effect = permission.Effect;
                row.IsEnabled = true;
                row.IsDeleted = false;
                row.ModifiedAt = DateTime.UtcNow;
                dataContext.Update(row);
                continue;
            }

            dataContext.Add(new IdentityRoleFeaturePermissionOverride
            {
                Id = Guid.NewGuid(),
                TenantId = role.Data.TenantId,
                IdentityRoleId = role.Data.Id,
                ModuleKey = permission.ModuleKey,
                SubFeatureKey = permission.SubFeatureKey,
                CapabilityKey = permission.CapabilityKey,
                Effect = permission.Effect,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }

        foreach (var row in existing.Where(row => !desiredKeys.Contains(BuildPermissionKey(row))))
        {
            row.IsEnabled = false;
            row.IsDeleted = true;
            row.DeletedAt = DateTime.UtcNow;
            dataContext.Update(row);
        }

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<CredentialRolePermissionOverridesResponse>.Failure("Credential role overrides could not be saved", saveResult.StatusCode);

        return await GetCredentialRolePermissionOverridesAsync(new GetCredentialRolePermissionOverridesRequest
        {
            IdentityRoleId = request.IdentityRoleId,
            Metadata = request.Metadata
        }, ct);
    }

    public async Task<Result<IdentityRole>> AssignCredentialRoleAsync(
        AssignCredentialRoleRequest request,
        CancellationToken ct = default)
    {
        var credential = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == request.CredentialId)
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (credential is null)
            return Result<IdentityRole>.NotFound("Credential not found");

        if (request.Metadata.TenantId is { } metadataTenantId && metadataTenantId != credential.TenantId)
            return Result<IdentityRole>.Failure("Credential does not belong to the active tenant", 403);

        var authorization = await EnsureCallerCapabilityAsync(
            request.Metadata,
            credential.TenantId,
            TenantModuleFeatureKeys.Identity,
            TenantModuleFeatureKeys.RolesSubFeature,
            IdentityAuthorizationConstants.Create,
            ct);
        if (!authorization.IsSuccess)
            return Result<IdentityRole>.Failure(authorization.Message!, authorization.StatusCode);

        var roleType = await GetRoleTypeAsync(request.RoleTypeId, credential.TenantId, ct);
        if (!roleType.IsSuccess)
            return Result<IdentityRole>.Failure(roleType.Message!, roleType.StatusCode);
        var roleTypeData = roleType.Data!;

        var existing = await dataContext.Query<IdentityRole>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == credential.TenantId)
            .Where(x => x.CredentialId == credential.Id)
            .Where(x => x.TypeId == roleTypeData.Id)
            .FirstOrDefaultAsync(ct);

        var expiration = request.RoleExpiration == default
            ? DateTime.UtcNow.AddYears(1)
            : request.RoleExpiration.ToUniversalTime();

        if (existing is null)
        {
            existing = new IdentityRole
            {
                Id = Guid.NewGuid(),
                TenantId = credential.TenantId,
                CredentialId = credential.Id,
                TypeId = roleTypeData.Id,
                RoleExpiration = expiration,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };
            dataContext.Add(existing);
        }
        else
        {
            existing.RoleExpiration = expiration;
            existing.IsEnabled = true;
            existing.IsDeleted = false;
            existing.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(existing);
        }

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<IdentityRole>.Failure("Role could not be assigned", saveResult.StatusCode);

        existing.Type = roleTypeData;
        existing.Credential = credential;
        return Result<IdentityRole>.Success(existing);
    }

    public async Task<Result> RemoveCredentialRoleAsync(
        RemoveCredentialRoleRequest request,
        CancellationToken ct = default)
    {
        var role = await GetIdentityRoleAsync(request.IdentityRoleId, request.Metadata.TenantId, ct);
        if (!role.IsSuccess)
            return Result.Failure(role.Message!, role.StatusCode);

        var authorization = await EnsureCallerCapabilityAsync(
            request.Metadata,
            role.Data!.TenantId,
            TenantModuleFeatureKeys.Identity,
            TenantModuleFeatureKeys.RolesSubFeature,
            IdentityAuthorizationConstants.Delete,
            ct);
        if (!authorization.IsSuccess)
            return Result.Failure(authorization.Message!, authorization.StatusCode);

        role.Data!.IsEnabled = false;
        role.Data.IsDeleted = true;
        role.Data.DeletedAt = DateTime.UtcNow;
        dataContext.Update(role.Data);

        var overrides = await dataContext.Query<IdentityRoleFeaturePermissionOverride>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == role.Data.TenantId)
            .Where(x => x.IdentityRoleId == role.Data.Id)
            .Where(x => !x.IsDeleted)
            .ToListAsync(ct);

        foreach (var permissionOverride in overrides)
        {
            permissionOverride.IsEnabled = false;
            permissionOverride.IsDeleted = true;
            permissionOverride.DeletedAt = DateTime.UtcNow;
            dataContext.Update(permissionOverride);
        }

        var saveResult = await dataContext.SaveChangesAsync(ct);
        return saveResult.IsSuccess
            ? Result.Success("Role removed")
            : Result.Failure("Role could not be removed", saveResult.StatusCode);
    }

    public async Task<Result> SeedRoleTypePermissionsAsync(
        Guid tenantId,
        Guid roleTypeId,
        CancellationToken ct = default)
    {
        var roleType = await GetRoleTypeAsync(roleTypeId, tenantId, ct);
        if (!roleType.IsSuccess)
            return Result.Failure(roleType.Message!, roleType.StatusCode);

        var allPermissions = TenantModuleFeatureKeys.All
            .SelectMany(feature => IdentityAuthorizationConstants.CapabilityKeys.Select(capability =>
                new CapabilityPermissionDto
                {
                    ModuleKey = feature.ModuleKey,
                    SubFeatureKey = feature.SubFeatureKey,
                    CapabilityKey = capability,
                    Effect = RoleCapabilityPermissionEffect.Allow,
                    IsEnabled = true
                }))
            .ToList();

        var existing = await dataContext.Query<IdentityRoleTypeFeaturePermission>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.RoleTypeId == roleTypeId)
            .ToListAsync(ct);

        var existingByKey = existing.ToDictionary(BuildPermissionKey, StringComparer.OrdinalIgnoreCase);

        foreach (var permission in allPermissions)
        {
            if (existingByKey.TryGetValue(BuildPermissionKey(permission), out var row))
            {
                row.Effect = RoleCapabilityPermissionEffect.Allow;
                row.IsEnabled = true;
                row.IsDeleted = false;
                row.ModifiedAt = DateTime.UtcNow;
                dataContext.Update(row);
                continue;
            }

            dataContext.Add(new IdentityRoleTypeFeaturePermission
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RoleTypeId = roleTypeId,
                ModuleKey = permission.ModuleKey,
                SubFeatureKey = permission.SubFeatureKey,
                CapabilityKey = permission.CapabilityKey,
                Effect = RoleCapabilityPermissionEffect.Allow,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }

        var saveResult = await dataContext.SaveChangesAsync(ct);
        return saveResult.IsSuccess
            ? Result.Success("Role type permissions seeded")
            : Result.Failure("Role type permissions could not be seeded", saveResult.StatusCode);
    }

    private async Task<CapabilityDecision> ResolveCapabilityAsync(
        Guid tenantId,
        Guid credentialId,
        string moduleKey,
        string subFeatureKey,
        string capabilityKey,
        CancellationToken ct,
        bool credentialAlreadyValidated = false)
    {
        if (!credentialAlreadyValidated)
        {
            var credentialExists = await dataContext.Query<IdentityCredential>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(x => x.TenantId == tenantId)
                .Where(x => x.Id == credentialId)
                .Where(x => !x.IsDeleted && x.IsEnabled)
                .AnyAsync(ct);

            if (!credentialExists)
                return new CapabilityDecision(false, "CredentialNotFound");
        }

        if (RequiresTenantFeature(moduleKey, subFeatureKey))
        {
            var tenantFeature = await dataContext.Query<TenantModuleFeature>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(x => x.TenantId == tenantId)
                .Where(x => x.ModuleKey == moduleKey)
                .Where(x => x.SubFeatureKey == subFeatureKey)
                .Where(x => !x.IsDeleted && x.IsEnabled)
                .FirstOrDefaultAsync(ct);

            if (tenantFeature is null)
                return new CapabilityDecision(false, SourceTenantFeatureDisabled);
        }

        var now = DateTime.UtcNow;
        var activeRoles = await dataContext.Query<IdentityRole>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.CredentialId == credentialId)
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .Where(x => x.TypeId != null)
            .Where(x => x.RoleExpiration >= now)
            .ToListAsync(ct);

        if (activeRoles.Count == 0)
            return new CapabilityDecision(false, "NoActiveRole");

        var roleIds = activeRoles.Select(x => x.Id).ToArray();
        var roleTypeIds = activeRoles
            .Select(x => x.TypeId!.Value)
            .Distinct()
            .ToArray();
        var capabilityKeys = BuildCapabilityKeySet(capabilityKey);

        var overrides = await dataContext.Query<IdentityRoleFeaturePermissionOverride>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => roleIds.Contains(x.IdentityRoleId))
            .Where(x => x.ModuleKey == moduleKey)
            .Where(x => x.SubFeatureKey == subFeatureKey)
            .Where(x => capabilityKeys.Contains(x.CapabilityKey))
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .ToListAsync(ct);

        var roleTypePermissions = await dataContext.Query<IdentityRoleTypeFeaturePermission>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => roleTypeIds.Contains(x.RoleTypeId))
            .Where(x => x.ModuleKey == moduleKey)
            .Where(x => x.SubFeatureKey == subFeatureKey)
            .Where(x => capabilityKeys.Contains(x.CapabilityKey))
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .ToListAsync(ct);

        var decisions = new List<CapabilityDecision>();
        foreach (var role in activeRoles)
        {
            var roleOverrides = overrides
                .Where(x => x.IdentityRoleId == role.Id)
                .OrderBy(x => string.Equals(x.CapabilityKey, capabilityKey, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
            if (roleOverrides.Count > 0)
            {
                decisions.AddRange(roleOverrides.Select(x => ToDecision(x.Effect, isOverride: true)));
                continue;
            }

            var roleTypePermissionMatches = roleTypePermissions
                .Where(x => x.RoleTypeId == role.TypeId)
                .OrderBy(x => string.Equals(x.CapabilityKey, capabilityKey, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
            if (roleTypePermissionMatches.Count > 0)
            {
                decisions.AddRange(roleTypePermissionMatches.Select(x => ToDecision(x.Effect, isOverride: false)));
            }
        }

        if (decisions.Any(x => !x.IsAllowed))
            return decisions.First(x => !x.IsAllowed);

        if (decisions.Any(x => x.IsAllowed))
            return decisions.First(x => x.IsAllowed);

        return await ResolveMissingPermissionAsync(tenantId, ct);
    }

    private async Task<CapabilityDecision> ResolveMissingPermissionAsync(Guid tenantId, CancellationToken ct)
    {
        var policy = await dataContext.Query<TenantAuthorizationPolicy>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);

        var behavior = policy?.MissingPermissionBehavior ?? MissingPermissionBehavior.Deny;

        return behavior == MissingPermissionBehavior.Allow
            ? new CapabilityDecision(true, SourceTenantDefaultAllow)
            : new CapabilityDecision(false, SourceTenantDefaultDeny);
    }

    private async Task<Result<IdentityRoleType>> GetRoleTypeAsync(
        Guid roleTypeId,
        Guid? tenantId,
        CancellationToken ct)
    {
        if (roleTypeId == Guid.Empty)
            return Result<IdentityRoleType>.Failure("Role type is required", 400);

        if (tenantId is null || tenantId == Guid.Empty)
            return Result<IdentityRoleType>.Forbidden("Tenant metadata is required");

        var roleType = await dataContext.Query<IdentityRoleType>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == roleTypeId)
            .Where(x => x.TenantId == tenantId.Value)
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (roleType is null)
            return Result<IdentityRoleType>.NotFound("Role type not found");

        return Result<IdentityRoleType>.Success(roleType);
    }

    private async Task<Result<IdentityRole>> GetIdentityRoleAsync(
        Guid roleId,
        Guid? tenantId,
        CancellationToken ct)
    {
        if (roleId == Guid.Empty)
            return Result<IdentityRole>.Failure("Identity role is required", 400);

        if (tenantId is null || tenantId == Guid.Empty)
            return Result<IdentityRole>.Forbidden("Tenant metadata is required");

        var role = await dataContext.Query<IdentityRole>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == roleId)
            .Where(x => x.TenantId == tenantId.Value)
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (role is null)
            return Result<IdentityRole>.NotFound("Identity role not found");

        return Result<IdentityRole>.Success(role);
    }

    private async Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken ct) =>
        await dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == tenantId)
            .Where(x => !x.IsDeleted)
            .AnyAsync(ct);

    private static Result<Guid> ResolveRequestedTenantId(Guid requestedTenantId, Guid? metadataTenantId)
    {
        if (metadataTenantId is null || metadataTenantId == Guid.Empty)
            return Result<Guid>.Forbidden("Tenant metadata is required");

        if (requestedTenantId != Guid.Empty && requestedTenantId != metadataTenantId.Value)
            return Result<Guid>.Failure("Requested tenant does not match the active tenant", 403);

        return Result<Guid>.Success(metadataTenantId.Value);
    }

    private async Task<Result> EnsureCanInspectCredentialCapabilitiesAsync(
        RequestMetadata metadata,
        Guid targetTenantId,
        Guid targetCredentialId,
        CancellationToken ct)
    {
        var tenantCheck = EnsureMetadataTenantMatchesTarget(metadata, targetTenantId);
        if (!tenantCheck.IsSuccess)
            return tenantCheck;

        if (TryResolveAuthenticatedHttpCredential(metadata, targetTenantId, out var credentialId) &&
            credentialId == targetCredentialId)
        {
            return Result.Success();
        }

        return await EnsureCallerCapabilityAsync(
            metadata,
            targetTenantId,
            TenantModuleFeatureKeys.Identity,
            TenantModuleFeatureKeys.RolesSubFeature,
            IdentityAuthorizationConstants.View,
            ct);
    }

    private async Task<Result> EnsureCallerCapabilityAsync(
        RequestMetadata metadata,
        Guid targetTenantId,
        string moduleKey,
        string subFeatureKey,
        string capabilityKey,
        CancellationToken ct)
    {
        var tenantCheck = EnsureMetadataTenantMatchesTarget(metadata, targetTenantId);
        if (!tenantCheck.IsSuccess)
            return tenantCheck;

        if (TryResolveAuthenticatedHttpCredential(metadata, targetTenantId, out var credentialId))
        {
            var decision = await ResolveCapabilityAsync(
                targetTenantId,
                credentialId,
                moduleKey,
                subFeatureKey,
                capabilityKey,
                ct);

            if (decision.IsAllowed)
                return Result.Success();
        }

        var trustedInvocation = await trustedServiceInvocationResolver.ResolveAsync(
            metadata,
            XFrameworkServiceNames.IdentityServer,
            [XFrameworkServiceScopes.IdentityAdmin],
            requireTenant: true,
            ct: ct);

        if (trustedInvocation.IsSuccess)
        {
            return trustedInvocation.Invocation?.TenantId == targetTenantId
                ? Result.Success()
                : Result.Forbidden("Trusted service tenant does not match the active tenant");
        }

        logger.LogWarning(
            "Identity authorization denied for tenant {TenantId}, module {ModuleKey}, subfeature {SubFeatureKey}, capability {CapabilityKey}: {Reason}",
            targetTenantId,
            moduleKey,
            subFeatureKey,
            capabilityKey,
            trustedInvocation.Error ?? "Caller capability was not allowed");

        return Result.Forbidden("Caller is not allowed to manage IdentityServer authorization");
    }

    private static Result EnsureMetadataTenantMatchesTarget(RequestMetadata metadata, Guid targetTenantId)
    {
        if (metadata.TenantId is null || metadata.TenantId == Guid.Empty)
            return Result.Forbidden("Tenant metadata is required");

        return metadata.TenantId.Value == targetTenantId
            ? Result.Success()
            : Result.Forbidden("Requested tenant does not match the active tenant");
    }

    private bool TryResolveAuthenticatedHttpCredential(
        RequestMetadata metadata,
        Guid targetTenantId,
        out Guid credentialId)
    {
        credentialId = default;

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        var claimTenantId = ResolveGuidClaim(user, "tenant_id", "tenantId", "TenantId", "tid", "tenant");
        var claimCredentialId = ResolveGuidClaim(
            user,
            "credentialId",
            "credential_id",
            ClaimTypes.NameIdentifier,
            "sub");

        if (claimTenantId != targetTenantId ||
            claimCredentialId is null ||
            claimCredentialId == Guid.Empty ||
            metadata.TenantId != claimTenantId ||
            metadata.CredentialId != claimCredentialId)
        {
            return false;
        }

        credentialId = claimCredentialId.Value;
        return true;
    }

    private static Guid? ResolveGuidClaim(ClaimsPrincipal user, params string[] claimNames)
    {
        foreach (var claimName in claimNames)
        {
            var claimValue = user.FindFirst(claimName)?.Value;
            if (Guid.TryParse(claimValue, out var value))
                return value;
        }

        return null;
    }

    private static Result<PermissionTarget> NormalizePermissionTarget(
        string? moduleKey,
        string? subFeatureKey,
        string? capabilityKey)
    {
        var (normalizedModuleKey, normalizedSubFeatureKey) =
            TenantModuleFeatureKeys.Normalize(moduleKey ?? string.Empty, subFeatureKey);

        if (string.IsNullOrWhiteSpace(normalizedModuleKey))
            return Result<PermissionTarget>.Failure("Module key is required", 400);

        var normalizedCapability = NormalizeCapabilityKey(capabilityKey);
        if (normalizedCapability is null)
            return Result<PermissionTarget>.Failure("Capability key is required", 400);

        return Result<PermissionTarget>.Success(
            new PermissionTarget(normalizedModuleKey, normalizedSubFeatureKey, normalizedCapability));
    }

    private static Result<List<CapabilityPermissionDto>> NormalizePermissionDtos(
        IEnumerable<CapabilityPermissionDto> permissions)
    {
        var rows = new Dictionary<string, CapabilityPermissionDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in permissions)
        {
            var normalized = NormalizePermissionTarget(permission.ModuleKey, permission.SubFeatureKey, permission.CapabilityKey);
            if (!normalized.IsSuccess)
                return Result<List<CapabilityPermissionDto>>.Failure(normalized.Message!, normalized.StatusCode);

            if (!Enum.IsDefined(permission.Effect))
                return Result<List<CapabilityPermissionDto>>.Failure("Permission effect is invalid", 400);

            var dto = permission with
            {
                ModuleKey = normalized.Data!.ModuleKey,
                SubFeatureKey = normalized.Data.SubFeatureKey,
                CapabilityKey = normalized.Data.CapabilityKey,
                IsEnabled = true
            };

            rows[BuildPermissionKey(dto)] = dto;
        }

        return Result<List<CapabilityPermissionDto>>.Success(rows.Values.ToList());
    }

    private static string? NormalizeCapabilityKey(string? capabilityKey)
    {
        var normalized = capabilityKey?.Trim().ToLowerInvariant();
        return IdentityAuthorizationConstants.CapabilityKeys.Contains(normalized)
            ? normalized
            : null;
    }

    private static bool RequiresTenantFeature(string moduleKey, string subFeatureKey) =>
        !string.Equals(moduleKey, TenantModuleFeatureKeys.Identity, StringComparison.OrdinalIgnoreCase) ||
        !string.IsNullOrWhiteSpace(subFeatureKey);

    private static string[] BuildCapabilityKeySet(string capabilityKey)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            capabilityKey
        };

        if (!string.Equals(capabilityKey, IdentityAuthorizationConstants.Manage, StringComparison.OrdinalIgnoreCase))
        {
            keys.Add(IdentityAuthorizationConstants.Manage);
        }

        return keys.ToArray();
    }

    private static CapabilityDecision ToDecision(RoleCapabilityPermissionEffect effect, bool isOverride) =>
        effect == RoleCapabilityPermissionEffect.Allow
            ? new CapabilityDecision(true, isOverride ? SourceCredentialOverrideAllow : SourceRoleTypeAllow)
            : new CapabilityDecision(false, isOverride ? SourceCredentialOverrideDeny : SourceRoleTypeDeny);

    private static CapabilityPermissionDto ToPermissionDto(IdentityRoleTypeFeaturePermission permission) => new()
    {
        Id = permission.Id,
        ModuleKey = permission.ModuleKey,
        SubFeatureKey = permission.SubFeatureKey,
        CapabilityKey = permission.CapabilityKey,
        Effect = permission.Effect,
        IsEnabled = permission.IsEnabled
    };

    private static CapabilityPermissionDto ToPermissionDto(IdentityRoleFeaturePermissionOverride permission) => new()
    {
        Id = permission.Id,
        ModuleKey = permission.ModuleKey,
        SubFeatureKey = permission.SubFeatureKey,
        CapabilityKey = permission.CapabilityKey,
        Effect = permission.Effect,
        IsEnabled = permission.IsEnabled
    };

    private static string BuildPermissionKey(CapabilityPermissionDto permission) =>
        $"{permission.ModuleKey}:{permission.SubFeatureKey}:{permission.CapabilityKey}";

    private static string BuildPermissionKey(IdentityRoleTypeFeaturePermission permission) =>
        $"{permission.ModuleKey}:{permission.SubFeatureKey}:{permission.CapabilityKey}";

    private static string BuildPermissionKey(IdentityRoleFeaturePermissionOverride permission) =>
        $"{permission.ModuleKey}:{permission.SubFeatureKey}:{permission.CapabilityKey}";

    private sealed record PermissionTarget(string ModuleKey, string SubFeatureKey, string CapabilityKey);

    private sealed record CapabilityDecision(bool IsAllowed, string Source);
}
