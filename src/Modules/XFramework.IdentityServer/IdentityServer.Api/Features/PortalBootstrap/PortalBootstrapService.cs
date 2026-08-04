using IdentityServer.Domain.Shared;
using IdentityServer.Api.Infrastructure;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Enums;

namespace IdentityServer.Api.Features.PortalBootstrap;

public sealed class PortalBootstrapService
{
    private const string BootstrapLockSql =
        "SELECT pg_advisory_xact_lock(hashtextextended('identity:portal-bootstrap-admin', 0))";

    private static readonly Guid AdminTenantId = PortalBootstrapConstants.AdminTenantId;
    private static readonly Guid AdminIdentityId = new("e9c13537-9726-4508-94e6-62806cb706f1");
    private static readonly Guid AdminCredentialId = new("2f63b5be-c45d-4b9f-80a5-39549341f417");
    private static readonly Guid AdminRoleGroupId = new("f5149783-49dd-4c88-8f3e-33c07f2f7797");
    private static readonly Guid AdminRoleTypeId = new("14524d87-582d-4af6-8d6c-4f58ffad34f5");
    private static readonly Guid AdminRoleId = new("55696c56-73e6-4648-8475-58749f87d20a");
    private static readonly Guid UserSessionTypeId = new("87c0ed1a-1243-4bbb-ae2b-f3350f5d0376");
    private static readonly Guid ServiceSessionTypeId = new("a97c6233-4ef2-42b8-a8cb-52e65bedf15c");
    private static readonly Guid RpcSessionTypeId = new("aa1a2abf-d1d3-4fc4-b5b0-f04f47656c72");
    private static readonly Guid RegistryGroupId = new("64a55f7d-7b19-42c9-94bb-61130cd65f97");
    private static readonly Guid DefaultAuthorizeById = new("934ce9b2-6513-411b-a32f-5dc74f61975c");
    private static readonly Guid AdminRoleGroupSystemReferenceId = new("1208681c-a202-453b-95f2-f0cbf682f9dd");
    private static readonly Guid RegistryGroupSystemReferenceId = new("35ab856b-7f99-4d2e-ac46-e5093bb27b59");
    private static readonly string[] LegacyAdminTenantNames = ["XFramework Admin"];

    private static readonly (Guid Id, string Name, Guid SystemReferenceId)[] RequiredSessionTypes =
    [
        (UserSessionTypeId, "User", IdentityConstants.SessionType.User),
        (ServiceSessionTypeId, "Service", IdentityConstants.SessionType.Service),
        (RpcSessionTypeId, "Rpc", IdentityConstants.SessionType.Rpc)
    ];

    public static async Task<Result<PortalBootstrapAdminResponse>> EnsureAdminAsync(
        EnsurePortalBootstrapAdminRequest request,
        AppDbContext db,
        ILogger<PortalBootstrapService> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<PortalBootstrapAdminResponse>.Failure(
                "Password is required",
                StatusCodes.Status400BadRequest);
        }

        if (!IdentityPasswordPolicy.IsWithinBcryptByteLimit(request.Password))
        {
            return Result<PortalBootstrapAdminResponse>.Failure(
                "Password must not exceed 72 UTF-8 bytes",
                StatusCodes.Status400BadRequest);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync(BootstrapLockSql, ct);

        var now = DateTime.UtcNow;
        var created = false;
        var tenantName = request.TenantName.Trim();
        var userName = request.UserName.Trim();
        var displayName = request.DisplayName.Trim();
        var tenantNames = LegacyAdminTenantNames
            .Append(tenantName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var tenant = await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.Id == AdminTenantId, ct);
        tenant ??= await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(item => tenantNames.Contains(item.Name))
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = AdminTenantId,
                TenantId = AdminTenantId,
                Name = tenantName,
                Description = "Portal bootstrap administration tenant",
                Status = 1,
                Version = 1m,
                IsEnabled = true,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            };
            db.Add(tenant);
            created = true;
        }
        else if (!IsUsable(tenant))
        {
            return Result<PortalBootstrapAdminResponse>.Conflict(
                "The Portal bootstrap tenant is disabled or deleted");
        }

        var tenantId = tenant.Id;
        var roleGroupResult = await EnsureRoleGroupAsync(db, tenantId, now, ct);
        if (!roleGroupResult.IsSuccess)
            return Result<PortalBootstrapAdminResponse>.Failure(roleGroupResult.Message!, roleGroupResult.StatusCode);
        var roleGroup = roleGroupResult.Data!;
        created |= roleGroup.Created;

        var roleTypeResult = await EnsureRoleTypeAsync(db, tenantId, roleGroup.Entity.Id, now, ct);
        if (!roleTypeResult.IsSuccess)
            return Result<PortalBootstrapAdminResponse>.Failure(roleTypeResult.Message!, roleTypeResult.StatusCode);
        var roleType = roleTypeResult.Data!;
        created |= roleType.Created;

        created |= await EnsureSessionTypesAsync(db, tenantId, now, ct);
        created |= await EnsureIdentityFeaturesAsync(db, tenantId, now, ct);

        var registryResult = await EnsureRegistryConfigurationAsync(db, tenantId, now, ct);
        if (!registryResult.IsSuccess)
            return Result<PortalBootstrapAdminResponse>.Failure(registryResult.Message!, registryResult.StatusCode);
        created |= registryResult.Data;

        var identityResult = await EnsureIdentityAsync(db, tenantId, displayName, now, ct);
        if (!identityResult.IsSuccess)
            return Result<PortalBootstrapAdminResponse>.Failure(identityResult.Message!, identityResult.StatusCode);
        var identity = identityResult.Data!;
        created |= identity.Created;

        var credentialResult = await EnsureCredentialAsync(
            db,
            tenantId,
            identity.Entity.Id,
            userName,
            request.Password,
            now,
            ct);
        if (!credentialResult.IsSuccess)
            return Result<PortalBootstrapAdminResponse>.Failure(credentialResult.Message!, credentialResult.StatusCode);
        var credential = credentialResult.Data!;
        created |= credential.Created;

        var roleResult = await EnsureRoleAsync(
            db,
            tenantId,
            credential.Entity.Id,
            roleType.Entity.Id,
            now,
            ct);
        if (!roleResult.IsSuccess)
            return Result<PortalBootstrapAdminResponse>.Failure(roleResult.Message!, roleResult.StatusCode);
        var role = roleResult.Data!;
        created |= role.Created;

        created |= await EnsureAuthorizationAsync(db, tenantId, roleType.Entity, now, ct);

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Portal bootstrap administration records could not be saved.");
            return Result<PortalBootstrapAdminResponse>.Conflict(
                "Portal bootstrap administration records could not be saved");
        }

        logger.LogInformation(
            "Portal bootstrap administrator ensured for tenant {TenantId} and credential {CredentialId}.",
            tenantId,
            credential.Entity.Id);

        return Result<PortalBootstrapAdminResponse>.Success(new PortalBootstrapAdminResponse
        {
            TenantId = tenantId,
            IdentityId = identity.Entity.Id,
            CredentialId = credential.Entity.Id,
            RoleTypeId = roleType.Entity.Id,
            RoleId = role.Entity.Id,
            TenantName = tenant.Name ?? tenantName,
            UserName = credential.Entity.UserName ?? userName,
            Created = created
        });
    }

    private static async Task<Result<EnsuredEntity<IdentityRoleTypeGroup>>> EnsureRoleGroupAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken ct)
    {
        var group = await db.Set<IdentityRoleTypeGroup>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.Id == AdminRoleGroupId, ct);
        group ??= await db.Set<IdentityRoleTypeGroup>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.SystemReferenceId == AdminRoleGroupSystemReferenceId, ct);

        if (group is not null)
        {
            return group.TenantId == tenantId && IsUsable(group)
                ? Result<EnsuredEntity<IdentityRoleTypeGroup>>.Success(new(group, false))
                : Result<EnsuredEntity<IdentityRoleTypeGroup>>.Conflict("The Portal administrator role group is invalid");
        }

        group = new IdentityRoleTypeGroup
        {
            Id = AdminRoleGroupId,
            TenantId = tenantId,
            Name = "Administrators",
            Description = "Portal administrator roles",
            SystemReferenceId = AdminRoleGroupSystemReferenceId,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Add(group);
        return Result<EnsuredEntity<IdentityRoleTypeGroup>>.Success(new(group, true));
    }

    private static async Task<Result<EnsuredEntity<IdentityRoleType>>> EnsureRoleTypeAsync(
        AppDbContext db,
        Guid tenantId,
        Guid groupId,
        DateTime now,
        CancellationToken ct)
    {
        var roleType = await db.Set<IdentityRoleType>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.Id == AdminRoleTypeId, ct);
        roleType ??= await db.Set<IdentityRoleType>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.SystemReferenceId == IdentityConstants.RoleType.Admin, ct);

        if (roleType is not null)
        {
            return roleType.TenantId == tenantId && roleType.GroupId == groupId && IsUsable(roleType)
                ? Result<EnsuredEntity<IdentityRoleType>>.Success(new(roleType, false))
                : Result<EnsuredEntity<IdentityRoleType>>.Conflict("The Portal administrator role type is invalid");
        }

        roleType = new IdentityRoleType
        {
            Id = AdminRoleTypeId,
            TenantId = tenantId,
            Name = "Portal Super Admin",
            RoleLevel = 100,
            GroupId = groupId,
            SystemReferenceId = IdentityConstants.RoleType.Admin,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Add(roleType);
        return Result<EnsuredEntity<IdentityRoleType>>.Success(new(roleType, true));
    }

    private static async Task<bool> EnsureSessionTypesAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken ct)
    {
        var sessionTypes = await db.Set<SessionType>()
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(item => item.TenantId == tenantId)
            .Where(item => RequiredSessionTypes.Select(required => required.SystemReferenceId)
                .Contains(item.SystemReferenceId))
            .ToListAsync(ct);
        var created = false;

        foreach (var required in RequiredSessionTypes)
        {
            if (sessionTypes.Any(item => item.SystemReferenceId == required.SystemReferenceId))
                continue;

            db.Add(new SessionType
            {
                Id = required.Id,
                TenantId = tenantId,
                Name = required.Name,
                SystemReferenceId = required.SystemReferenceId,
                IsEnabled = true,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            });
            created = true;
        }

        return created;
    }

    private static async Task<bool> EnsureIdentityFeaturesAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken ct)
    {
        var definitions = TenantModuleFeatureKeys.All
            .Where(feature => string.Equals(
                feature.ModuleKey,
                TenantModuleFeatureKeys.Identity,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var features = await db.Set<TenantModuleFeature>()
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(item => item.TenantId == tenantId && item.ModuleKey == TenantModuleFeatureKeys.Identity)
            .ToListAsync(ct);
        var created = false;

        foreach (var definition in definitions)
        {
            if (features.Any(item => string.Equals(
                    item.SubFeatureKey,
                    definition.SubFeatureKey,
                    StringComparison.OrdinalIgnoreCase)))
                continue;

            db.Add(new TenantModuleFeature
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ModuleKey = definition.ModuleKey,
                SubFeatureKey = definition.SubFeatureKey,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                IsEnabled = true,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            });
            created = true;
        }

        return created;
    }

    private static async Task<Result<bool>> EnsureRegistryConfigurationAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken ct)
    {
        var group = await db.Set<RegistryConfigurationGroup>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.Id == RegistryGroupId, ct);
        group ??= await db.Set<RegistryConfigurationGroup>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.SystemReferenceId == RegistryGroupSystemReferenceId, ct);
        var created = false;

        if (group is not null && (group.TenantId != tenantId || !IsUsable(group)))
            return Result<bool>.Conflict("The Portal authentication registry group is invalid");

        if (group is null)
        {
            group = new RegistryConfigurationGroup
            {
                Id = RegistryGroupId,
                TenantId = tenantId,
                Name = "Auth",
                Description = "Portal authentication defaults",
                SystemReferenceId = RegistryGroupSystemReferenceId,
                IsEnabled = true,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            };
            db.Add(group);
            created = true;
        }

        var configuration = await db.Set<RegistryConfiguration>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Key == "DefaultAuthorizeBy", ct);
        var expectedValue = ((int)AuthorizationType.Username).ToString();
        if (configuration is null)
        {
            db.Add(new RegistryConfiguration
            {
                Id = DefaultAuthorizeById,
                TenantId = tenantId,
                Key = "DefaultAuthorizeBy",
                Value = expectedValue,
                GroupId = group.Id,
                IsEnabled = true,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            });
            created = true;
        }
        else if (!IsUsable(configuration) || configuration.GroupId != group.Id)
        {
            return Result<bool>.Conflict("The Portal authentication registry configuration is invalid");
        }
        else if (!string.Equals(configuration.Value, expectedValue, StringComparison.Ordinal))
        {
            configuration.Value = expectedValue;
            configuration.ModifiedAt = now;
            configuration.ConcurrencyStamp = Guid.NewGuid();
        }

        return Result<bool>.Success(created);
    }

    private static async Task<Result<EnsuredEntity<IdentityInformation>>> EnsureIdentityAsync(
        AppDbContext db,
        Guid tenantId,
        string displayName,
        DateTime now,
        CancellationToken ct)
    {
        var identity = await db.Set<IdentityInformation>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.Id == AdminIdentityId, ct);
        identity ??= await db.Set<IdentityInformation>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.IdentityName == displayName, ct);

        if (identity is not null)
        {
            return identity.TenantId == tenantId && IsUsable(identity)
                ? Result<EnsuredEntity<IdentityInformation>>.Success(new(identity, false))
                : Result<EnsuredEntity<IdentityInformation>>.Conflict("The Portal administrator identity is invalid");
        }

        var displayNameParts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        identity = new IdentityInformation
        {
            Id = AdminIdentityId,
            TenantId = tenantId,
            FirstName = displayNameParts.ElementAtOrDefault(0) ?? displayName,
            LastName = displayNameParts.ElementAtOrDefault(1),
            IdentityName = displayName,
            IsVerified = true,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Add(identity);
        return Result<EnsuredEntity<IdentityInformation>>.Success(new(identity, true));
    }

    private static async Task<Result<EnsuredEntity<IdentityCredential>>> EnsureCredentialAsync(
        AppDbContext db,
        Guid tenantId,
        Guid identityId,
        string userName,
        string password,
        DateTime now,
        CancellationToken ct)
    {
        var credential = await db.Set<IdentityCredential>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.Id == AdminCredentialId, ct);
        credential ??= await db.Set<IdentityCredential>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserName == userName, ct);

        if (credential is not null)
        {
            return credential.TenantId == tenantId && credential.IdentityInfoId == identityId && IsUsable(credential)
                ? Result<EnsuredEntity<IdentityCredential>>.Success(new(credential, false))
                : Result<EnsuredEntity<IdentityCredential>>.Conflict("The Portal administrator credential is invalid");
        }

        credential = new IdentityCredential
        {
            Id = AdminCredentialId,
            TenantId = tenantId,
            IdentityInfoId = identityId,
            UserName = userName,
            UserAlias = userName,
            PasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(inputKey: password, workFactor: 11)),
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Add(credential);
        return Result<EnsuredEntity<IdentityCredential>>.Success(new(credential, true));
    }

    private static async Task<Result<EnsuredEntity<IdentityRole>>> EnsureRoleAsync(
        AppDbContext db,
        Guid tenantId,
        Guid credentialId,
        Guid roleTypeId,
        DateTime now,
        CancellationToken ct)
    {
        var role = await db.Set<IdentityRole>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.Id == AdminRoleId, ct);
        role ??= await db.Set<IdentityRole>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.CredentialId == credentialId && item.TypeId == roleTypeId, ct);

        if (role is not null)
        {
            return role.TenantId == tenantId && role.CredentialId == credentialId && role.TypeId == roleTypeId
                   && IsUsable(role) && role.RoleExpiration > now
                ? Result<EnsuredEntity<IdentityRole>>.Success(new(role, false))
                : Result<EnsuredEntity<IdentityRole>>.Conflict("The Portal administrator role is invalid");
        }

        role = new IdentityRole
        {
            Id = AdminRoleId,
            TenantId = tenantId,
            CredentialId = credentialId,
            TypeId = roleTypeId,
            RoleExpiration = now.AddYears(50),
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Add(role);
        return Result<EnsuredEntity<IdentityRole>>.Success(new(role, true));
    }

    private static async Task<bool> EnsureAuthorizationAsync(
        AppDbContext db,
        Guid tenantId,
        IdentityRoleType roleType,
        DateTime now,
        CancellationToken ct)
    {
        var changed = false;
        var policy = await db.Set<TenantAuthorizationPolicy>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId, ct);
        if (policy is null)
        {
            db.Add(new TenantAuthorizationPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MissingPermissionBehavior = MissingPermissionBehavior.Deny,
                IsEnabled = true,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            });
            changed = true;
        }
        else if (policy.MissingPermissionBehavior != MissingPermissionBehavior.Deny || !IsUsable(policy))
        {
            policy.MissingPermissionBehavior = MissingPermissionBehavior.Deny;
            policy.IsEnabled = true;
            policy.IsDeleted = false;
            policy.DeletedAt = null;
            policy.ModifiedAt = now;
            policy.ConcurrencyStamp = Guid.NewGuid();
            changed = true;
        }

        var permissions = await db.Set<IdentityRoleTypeFeaturePermission>()
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(item => item.TenantId == tenantId && item.RoleTypeId == roleType.Id)
            .ToListAsync(ct);

        foreach (var feature in TenantModuleFeatureKeys.All)
        {
            foreach (var capability in IdentityAuthorizationConstants.CapabilityKeys)
            {
                var permission = permissions.FirstOrDefault(item =>
                    string.Equals(item.ModuleKey, feature.ModuleKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.SubFeatureKey, feature.SubFeatureKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.CapabilityKey, capability, StringComparison.OrdinalIgnoreCase));
                if (permission is null)
                {
                    db.Add(new IdentityRoleTypeFeaturePermission
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        RoleTypeId = roleType.Id,
                        ModuleKey = feature.ModuleKey,
                        SubFeatureKey = feature.SubFeatureKey,
                        CapabilityKey = capability,
                        Effect = RoleCapabilityPermissionEffect.Allow,
                        IsEnabled = true,
                        CreatedAt = now,
                        ConcurrencyStamp = Guid.NewGuid()
                    });
                    changed = true;
                    continue;
                }

                if (permission.Effect == RoleCapabilityPermissionEffect.Allow && IsUsable(permission))
                    continue;

                permission.Effect = RoleCapabilityPermissionEffect.Allow;
                permission.IsEnabled = true;
                permission.IsDeleted = false;
                permission.DeletedAt = null;
                permission.ModifiedAt = now;
                permission.ConcurrencyStamp = Guid.NewGuid();
                changed = true;
            }
        }

        if (changed && db.Entry(roleType).State != EntityState.Added)
        {
            roleType.ModifiedAt = now;
            roleType.ConcurrencyStamp = Guid.NewGuid();
        }

        return changed;
    }

    private static bool IsUsable(BaseModel entity) => entity.IsEnabled && !entity.IsDeleted;

    private sealed record EnsuredEntity<T>(T Entity, bool Created) where T : BaseModel;
}
