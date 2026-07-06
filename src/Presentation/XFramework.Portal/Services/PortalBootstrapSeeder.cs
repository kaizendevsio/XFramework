using System.Text;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Portal.Services;

public sealed class PortalBootstrapSeeder(
    IDataContext dataContext,
    IIdentityServerServiceWrapper identityServer,
    RequestMetadata requestMetadata,
    ILogger<PortalBootstrapSeeder> logger)
{
    public async Task SeedAsync(PortalAuthOptions options, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var tenant = await EnsureTenant(options, ct);

        requestMetadata.TenantId = tenant.Id;

        var roleGroup = await EnsureRoleGroup(tenant.Id, now, ct);
        var roleType = await EnsureRoleType(tenant.Id, roleGroup.Id, now, ct);
        await EnsureSessionType(tenant.Id, now, ct);
        await EnsureDefaultAuthorizeBy(tenant.Id, now, ct);
        var identity = await EnsureIdentity(options, tenant.Id, now, ct);
        var credential = await EnsureCredential(options, tenant.Id, identity.Id, now, ct);
        await EnsureRole(tenant.Id, credential.Id, roleType.Id, now, ct);

        var result = await dataContext.SaveChangesAsync(ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Bootstrap admin seed failed: {result.Message}");
        }

        await EnsureAdminAuthorization(tenant.Id, roleType.Id, ct);

        logger.LogInformation(
            "Portal bootstrap admin ensured for tenant {TenantId} and credential {CredentialId}.",
            tenant.Id,
            credential.Id);
    }

    private async Task<Tenant> EnsureTenant(PortalAuthOptions options, CancellationToken ct)
    {
        var tenant = await dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == PortalBootstrapConstants.AdminTenantId)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (tenant is not null)
        {
            return tenant;
        }

        var lookupNames = PortalBootstrapConstants.BuildAdminTenantLookupNames(options.TenantName);
        tenant = await dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => lookupNames.Contains(x.Name))
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (tenant is not null)
        {
            return tenant;
        }

        var createResult = await identityServer.CreateTenant(new CreateTenantRequest
        {
            Name = options.TenantName,
            Description = "Portal bootstrap administration tenant",
            Status = 1,
            Version = 1.0m,
            Metadata = new RequestMetadata
            {
                Name = "Portal",
                RequestId = Guid.NewGuid()
            }
        });

        if (!createResult.IsSuccess)
        {
            throw new InvalidOperationException($"Portal bootstrap admin tenant could not be created: {createResult.Message}");
        }

        tenant = await dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Name == options.TenantName)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (tenant is null)
        {
            throw new InvalidOperationException("Portal bootstrap admin tenant was created but could not be loaded.");
        }

        return tenant;
    }

    private async Task<IdentityRoleTypeGroup> EnsureRoleGroup(Guid tenantId, DateTime now, CancellationToken ct)
    {
        var group = await dataContext.Query<IdentityRoleTypeGroup>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == PortalBootstrapConstants.AdminRoleGroupId)
            .FirstOrDefaultAsync(ct);

        group ??= await dataContext.Query<IdentityRoleTypeGroup>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.SystemReferenceId == PortalBootstrapConstants.AdminRoleGroupSystemReferenceId)
            .FirstOrDefaultAsync(ct);

        if (group is not null)
        {
            return group;
        }

        group = new IdentityRoleTypeGroup
        {
            Id = PortalBootstrapConstants.AdminRoleGroupId,
            TenantId = tenantId,
            Name = "Administrators",
            Description = "Portal administrator roles",
            SystemReferenceId = PortalBootstrapConstants.AdminRoleGroupSystemReferenceId,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(group);
        return group;
    }

    private async Task<IdentityRoleType> EnsureRoleType(
        Guid tenantId,
        Guid groupId,
        DateTime now,
        CancellationToken ct)
    {
        var roleType = await dataContext.Query<IdentityRoleType>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && x.SystemReferenceId == IdentityConstants.RoleType.Admin)
            .FirstOrDefaultAsync(ct);

        if (roleType is not null)
        {
            return roleType;
        }

        roleType = new IdentityRoleType
        {
            Id = PortalBootstrapConstants.AdminRoleTypeId,
            TenantId = tenantId,
            Name = "Portal Super Admin",
            RoleLevel = 100,
            GroupId = groupId,
            SystemReferenceId = IdentityConstants.RoleType.Admin,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(roleType);
        return roleType;
    }

    private async Task EnsureSessionType(Guid tenantId, DateTime now, CancellationToken ct)
    {
        var sessionType = await dataContext.Query<SessionType>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.SystemReferenceId == IdentityConstants.SessionType.User)
            .Where(x => x.Name == "User")
            .FirstOrDefaultAsync(ct);

        if (sessionType is not null)
        {
            return;
        }

        dataContext.Add(new SessionType
        {
            Id = PortalBootstrapConstants.UserSessionTypeId,
            TenantId = tenantId,
            Name = "User",
            SystemReferenceId = IdentityConstants.SessionType.User,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
    }

    private async Task EnsureDefaultAuthorizeBy(Guid tenantId, DateTime now, CancellationToken ct)
    {
        var group = await dataContext.Query<RegistryConfigurationGroup>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == PortalBootstrapConstants.RegistryGroupId)
            .FirstOrDefaultAsync(ct);

        group ??= await dataContext.Query<RegistryConfigurationGroup>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.SystemReferenceId == PortalBootstrapConstants.RegistryGroupSystemReferenceId)
            .FirstOrDefaultAsync(ct);

        if (group is null)
        {
            group = new RegistryConfigurationGroup
            {
                Id = PortalBootstrapConstants.RegistryGroupId,
                TenantId = tenantId,
                Name = "Auth",
                Description = "Portal authentication defaults",
                SystemReferenceId = PortalBootstrapConstants.RegistryGroupSystemReferenceId,
                IsEnabled = true,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            };
            dataContext.Add(group);
        }

        var config = await dataContext.Query<RegistryConfiguration>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.Key == "DefaultAuthorizeBy")
            .FirstOrDefaultAsync(ct);

        if (config is not null)
        {
            return;
        }

        dataContext.Add(new RegistryConfiguration
        {
            Id = PortalBootstrapConstants.DefaultAuthorizeById,
            TenantId = tenantId,
            Key = "DefaultAuthorizeBy",
            Value = ((int)AuthorizationType.Username).ToString(),
            GroupId = group.Id,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
    }

    private async Task<IdentityInformation> EnsureIdentity(
        PortalAuthOptions options,
        Guid tenantId,
        DateTime now,
        CancellationToken ct)
    {
        var identity = await dataContext.Query<IdentityInformation>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == PortalBootstrapConstants.AdminIdentityId)
            .FirstOrDefaultAsync(ct);

        identity ??= await dataContext.Query<IdentityInformation>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.IdentityName == options.DisplayName)
            .FirstOrDefaultAsync(ct);

        if (identity is not null)
        {
            return identity;
        }

        var displayNameParts = options.DisplayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        identity = new IdentityInformation
        {
            Id = PortalBootstrapConstants.AdminIdentityId,
            TenantId = tenantId,
            FirstName = displayNameParts.ElementAtOrDefault(0) ?? options.DisplayName,
            LastName = displayNameParts.ElementAtOrDefault(1),
            IdentityName = options.DisplayName,
            IsVerified = true,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(identity);
        return identity;
    }

    private async Task<IdentityCredential> EnsureCredential(
        PortalAuthOptions options,
        Guid tenantId,
        Guid identityId,
        DateTime now,
        CancellationToken ct)
    {
        var credential = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == PortalBootstrapConstants.AdminCredentialId)
            .FirstOrDefaultAsync(ct);

        credential ??= await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.UserName == options.UserName)
            .FirstOrDefaultAsync(ct);

        if (credential is not null)
        {
            return credential;
        }

        credential = new IdentityCredential
        {
            Id = PortalBootstrapConstants.AdminCredentialId,
            TenantId = tenantId,
            IdentityInfoId = identityId,
            UserName = options.UserName,
            UserAlias = options.UserName,
            PasswordByte = HashPassword(options.Password!),
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(credential);
        return credential;
    }

    private async Task EnsureRole(Guid tenantId, Guid credentialId, Guid roleTypeId, DateTime now, CancellationToken ct)
    {
        var role = await dataContext.Query<IdentityRole>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.CredentialId == credentialId)
            .Where(x => x.TypeId == roleTypeId)
            .FirstOrDefaultAsync(ct);

        if (role is not null)
        {
            return;
        }

        dataContext.Add(new IdentityRole
        {
            Id = PortalBootstrapConstants.AdminRoleId,
            TenantId = tenantId,
            CredentialId = credentialId,
            TypeId = roleTypeId,
            RoleExpiration = now.AddYears(50),
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
    }

    private async Task EnsureAdminAuthorization(Guid tenantId, Guid roleTypeId, CancellationToken ct)
    {
        var policyResult = await identityServer.UpdateTenantAuthorizationPolicy(new UpdateTenantAuthorizationPolicyRequest
        {
            TenantId = tenantId,
            MissingPermissionBehavior = MissingPermissionBehavior.Deny,
            Metadata = BuildMetadata(tenantId)
        });

        if (!policyResult.IsSuccess)
        {
            throw new InvalidOperationException($"Bootstrap admin authorization policy could not be saved: {policyResult.Message}");
        }

        var permissionResult = await identityServer.SetRoleTypePermissions(new SetRoleTypePermissionsRequest
        {
            RoleTypeId = roleTypeId,
            Permissions = BuildAdminPermissions(),
            Metadata = BuildMetadata(tenantId)
        });

        if (!permissionResult.IsSuccess)
        {
            throw new InvalidOperationException($"Bootstrap admin permissions could not be saved: {permissionResult.Message}");
        }
    }

    private static List<CapabilityPermissionDto> BuildAdminPermissions() =>
        TenantModuleFeatureKeys.All
            .SelectMany(feature => IdentityAuthorizationConstants.CapabilityKeys.Select(capability => new CapabilityPermissionDto
            {
                ModuleKey = feature.ModuleKey,
                SubFeatureKey = feature.SubFeatureKey,
                CapabilityKey = capability,
                Effect = RoleCapabilityPermissionEffect.Allow
            }))
            .ToList();

    private static RequestMetadata BuildMetadata(Guid tenantId) => new()
    {
        TenantId = tenantId,
        Name = "Portal",
        RequestId = Guid.NewGuid()
    };

    private static byte[] HashPassword(string password) =>
        Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(inputKey: password, workFactor: 11));
}
