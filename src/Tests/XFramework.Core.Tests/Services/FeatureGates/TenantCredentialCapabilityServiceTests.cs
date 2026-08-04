using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;

namespace XFramework.Core.Tests.Services.FeatureGates;

[TestFixture]
public sealed class TenantCredentialCapabilityServiceTests
{
    [Test]
    public async Task EnsureAllowedAsync_RoleTypePermissionAllowsRequestedCapability()
    {
        await using var db = CreateDbContext();
        var subject = SeedSubject(db);
        SeedRoleTypePermission(
            db,
            subject,
            IdentityAuthorizationConstants.View,
            RoleCapabilityPermissionEffect.Allow);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.EnsureAllowedAsync(
            subject.TenantId,
            subject.CredentialId,
            TenantModuleFeatureKeys.Identity,
            null,
            IdentityAuthorizationConstants.View);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task EnsureAllowedAsync_ManagePermissionAllowsSpecificCrudCapability()
    {
        await using var db = CreateDbContext();
        var subject = SeedSubject(db);
        SeedRoleTypePermission(
            db,
            subject,
            IdentityAuthorizationConstants.Manage,
            RoleCapabilityPermissionEffect.Allow);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.EnsureAllowedAsync(
            subject.TenantId,
            subject.CredentialId,
            TenantModuleFeatureKeys.Identity,
            null,
            IdentityAuthorizationConstants.Delete);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task EnsureAllowedAsync_CredentialOverrideDenyBeatsRoleTypeAllow()
    {
        await using var db = CreateDbContext();
        var subject = SeedSubject(db);
        SeedRoleTypePermission(
            db,
            subject,
            IdentityAuthorizationConstants.View,
            RoleCapabilityPermissionEffect.Allow);
        db.Set<IdentityRoleFeaturePermissionOverride>().Add(new IdentityRoleFeaturePermissionOverride
        {
            Id = Guid.NewGuid(),
            TenantId = subject.TenantId,
            IdentityRoleId = subject.RoleId,
            ModuleKey = TenantModuleFeatureKeys.Identity,
            SubFeatureKey = string.Empty,
            CapabilityKey = IdentityAuthorizationConstants.View,
            Effect = RoleCapabilityPermissionEffect.Deny,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.EnsureAllowedAsync(
            subject.TenantId,
            subject.CredentialId,
            TenantModuleFeatureKeys.Identity,
            null,
            IdentityAuthorizationConstants.View);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task EnsureAllowedAsync_MissingPermissionUsesTenantDefaultDeny()
    {
        await using var db = CreateDbContext();
        var subject = SeedSubject(db);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.EnsureAllowedAsync(
            subject.TenantId,
            subject.CredentialId,
            TenantModuleFeatureKeys.Identity,
            null,
            IdentityAuthorizationConstants.Create);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task EnsureAllowedAsync_NoActiveRoleDoesNotUseTenantDefaultAllow()
    {
        await using var db = CreateDbContext();
        var subject = SeedSubject(db);
        db.Set<IdentityRole>().Remove(db.Set<IdentityRole>().Local.Single(x => x.Id == subject.RoleId));
        db.Set<TenantAuthorizationPolicy>().Local.Single(x => x.TenantId == subject.TenantId)
            .MissingPermissionBehavior = MissingPermissionBehavior.Allow;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.EnsureAllowedAsync(
            subject.TenantId,
            subject.CredentialId,
            TenantModuleFeatureKeys.Identity,
            null,
            IdentityAuthorizationConstants.View);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [TestCase("tenant")]
    [TestCase("identity")]
    [TestCase("role-type")]
    public async Task EnsureAllowedAsync_DisabledLifecycleDependencyDoesNotAuthorize(string dependency)
    {
        await using var db = CreateDbContext();
        var subject = SeedSubject(db);
        SeedRoleTypePermission(
            db,
            subject,
            IdentityAuthorizationConstants.Manage,
            RoleCapabilityPermissionEffect.Allow);

        switch (dependency)
        {
            case "tenant":
                db.Set<Tenant>().Local.Single(x => x.Id == subject.TenantId).IsEnabled = false;
                break;
            case "identity":
                db.Set<IdentityInformation>().Local.Single(x => x.TenantId == subject.TenantId).IsEnabled = false;
                break;
            case "role-type":
                db.Set<IdentityRoleType>().Local.Single(x => x.Id == subject.RoleTypeId).IsEnabled = false;
                break;
        }

        await db.SaveChangesAsync();

        var result = await CreateService(db).EnsureAllowedAsync(
            subject.TenantId,
            subject.CredentialId,
            TenantModuleFeatureKeys.Identity,
            null,
            IdentityAuthorizationConstants.View);

        result.IsSuccess.Should().BeFalse();
    }

    private static AppDbContext CreateDbContext()
    {
        _ = typeof(IdentityCredential);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return TestTrustedPersistence.Create(options);
    }

    private static TenantCredentialCapabilityService CreateService(AppDbContext db) =>
        new(db, NullLogger<TenantCredentialCapabilityService>.Instance);

    private static AuthorizationSubject SeedSubject(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var identityInfoId = Guid.NewGuid();

        db.Set<Tenant>().Add(new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Name = "Capability tenant",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });

        db.Set<TenantAuthorizationPolicy>().Add(new TenantAuthorizationPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MissingPermissionBehavior = MissingPermissionBehavior.Deny,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });

        db.Set<IdentityInformation>().Add(new IdentityInformation
        {
            Id = identityInfoId,
            TenantId = tenantId,
            FirstName = "Capability",
            LastName = "User",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });

        db.Set<IdentityCredential>().Add(new IdentityCredential
        {
            Id = credentialId,
            TenantId = tenantId,
            IdentityInfoId = identityInfoId,
            UserName = $"capability-{Guid.NewGuid():N}",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });

        db.Set<IdentityRoleType>().Add(new IdentityRoleType
        {
            Id = roleTypeId,
            TenantId = tenantId,
            Name = "Capability Role",
            GroupId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });

        db.Set<IdentityRole>().Add(new IdentityRole
        {
            Id = roleId,
            TenantId = tenantId,
            CredentialId = credentialId,
            TypeId = roleTypeId,
            RoleExpiration = DateTime.UtcNow.AddYears(1),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });

        return new AuthorizationSubject(tenantId, credentialId, roleId, roleTypeId);
    }

    private static void SeedRoleTypePermission(
        AppDbContext db,
        AuthorizationSubject subject,
        string capability,
        RoleCapabilityPermissionEffect effect)
    {
        db.Set<IdentityRoleTypeFeaturePermission>().Add(new IdentityRoleTypeFeaturePermission
        {
            Id = Guid.NewGuid(),
            TenantId = subject.TenantId,
            RoleTypeId = subject.RoleTypeId,
            ModuleKey = TenantModuleFeatureKeys.Identity,
            SubFeatureKey = string.Empty,
            CapabilityKey = capability,
            Effect = effect,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    private sealed record AuthorizationSubject(
        Guid TenantId,
        Guid CredentialId,
        Guid RoleId,
        Guid RoleTypeId);
}
