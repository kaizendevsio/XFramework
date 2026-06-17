using System.Text;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;

namespace ControlPanel.Server.Services;

public sealed class ControlPanelBootstrapSeeder(
    IDataContext dataContext,
    ILogger<ControlPanelBootstrapSeeder> logger)
{
    public async Task SeedAsync(ControlPanelAuthOptions options, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var tenant = await EnsureTenant(options, now, ct);
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

        logger.LogInformation(
            "ControlPanel bootstrap admin ensured for tenant {TenantId} and credential {CredentialId}.",
            tenant.Id,
            credential.Id);
    }

    private async Task<Tenant> EnsureTenant(ControlPanelAuthOptions options, DateTime now, CancellationToken ct)
    {
        var tenant = await dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == ControlPanelBootstrapConstants.AdminTenantId)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        tenant ??= await dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Name == options.TenantName)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (tenant is not null)
        {
            if (!tenant.IsEnabled || tenant.IsDeleted)
            {
                tenant.IsEnabled = true;
                tenant.IsDeleted = false;
                tenant.DeletedAt = null;
                tenant.ModifiedAt = now;
                dataContext.Update(tenant);
            }

            return tenant;
        }

        tenant = new Tenant
        {
            Id = ControlPanelBootstrapConstants.AdminTenantId,
            TenantId = ControlPanelBootstrapConstants.AdminTenantId,
            Name = options.TenantName,
            Description = "ControlPanel bootstrap administration tenant",
            Status = 1,
            Version = 1.0m,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(tenant);
        return tenant;
    }

    private async Task<IdentityRoleTypeGroup> EnsureRoleGroup(Guid tenantId, DateTime now, CancellationToken ct)
    {
        var group = await dataContext.Query<IdentityRoleTypeGroup>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == ControlPanelBootstrapConstants.AdminRoleGroupId)
            .FirstOrDefaultAsync(ct);

        group ??= await dataContext.Query<IdentityRoleTypeGroup>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.SystemReferenceId == ControlPanelBootstrapConstants.AdminRoleGroupSystemReferenceId)
            .FirstOrDefaultAsync(ct);

        if (group is not null)
        {
            return group;
        }

        group = new IdentityRoleTypeGroup
        {
            Id = ControlPanelBootstrapConstants.AdminRoleGroupId,
            TenantId = tenantId,
            Name = "Administrators",
            Description = "ControlPanel administrator roles",
            SystemReferenceId = ControlPanelBootstrapConstants.AdminRoleGroupSystemReferenceId,
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
            Id = ControlPanelBootstrapConstants.AdminRoleTypeId,
            TenantId = tenantId,
            Name = "ControlPanel Super Admin",
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
            Id = ControlPanelBootstrapConstants.UserSessionTypeId,
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
            .Where(x => x.Id == ControlPanelBootstrapConstants.RegistryGroupId)
            .FirstOrDefaultAsync(ct);

        group ??= await dataContext.Query<RegistryConfigurationGroup>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.SystemReferenceId == ControlPanelBootstrapConstants.RegistryGroupSystemReferenceId)
            .FirstOrDefaultAsync(ct);

        if (group is null)
        {
            group = new RegistryConfigurationGroup
            {
                Id = ControlPanelBootstrapConstants.RegistryGroupId,
                TenantId = tenantId,
                Name = "Auth",
                Description = "ControlPanel authentication defaults",
                SystemReferenceId = ControlPanelBootstrapConstants.RegistryGroupSystemReferenceId,
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
            Id = ControlPanelBootstrapConstants.DefaultAuthorizeById,
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
        ControlPanelAuthOptions options,
        Guid tenantId,
        DateTime now,
        CancellationToken ct)
    {
        var identity = await dataContext.Query<IdentityInformation>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == ControlPanelBootstrapConstants.AdminIdentityId)
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
            Id = ControlPanelBootstrapConstants.AdminIdentityId,
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
        ControlPanelAuthOptions options,
        Guid tenantId,
        Guid identityId,
        DateTime now,
        CancellationToken ct)
    {
        var credential = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.Id == ControlPanelBootstrapConstants.AdminCredentialId)
            .FirstOrDefaultAsync(ct);

        credential ??= await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.UserName == options.UserName)
            .FirstOrDefaultAsync(ct);

        if (credential is not null)
        {
            if (!credential.IsEnabled || credential.PasswordByte is not { Length: > 0 })
            {
                credential.IsEnabled = true;
                credential.IsDeleted = false;
                credential.DeletedAt = null;
                credential.ModifiedAt = now;

                if (credential.PasswordByte is not { Length: > 0 })
                {
                    credential.PasswordByte = HashPassword(options.Password!);
                }

                dataContext.Update(credential);
            }

            return credential;
        }

        credential = new IdentityCredential
        {
            Id = ControlPanelBootstrapConstants.AdminCredentialId,
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
            if (!role.IsEnabled || role.RoleExpiration <= now)
            {
                role.IsEnabled = true;
                role.IsDeleted = false;
                role.DeletedAt = null;
                role.RoleExpiration = now.AddYears(50);
                role.ModifiedAt = now;
                dataContext.Update(role);
            }

            return;
        }

        dataContext.Add(new IdentityRole
        {
            Id = ControlPanelBootstrapConstants.AdminRoleId,
            TenantId = tenantId,
            CredentialId = credentialId,
            TypeId = roleTypeId,
            RoleExpiration = now.AddYears(50),
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
    }

    private static byte[] HashPassword(string password) =>
        Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(inputKey: password, workFactor: 11));
}
