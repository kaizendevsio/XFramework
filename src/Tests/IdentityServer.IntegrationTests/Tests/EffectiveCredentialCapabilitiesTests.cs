using System.Security.Claims;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
[Category(TestCategories.Wrappers)]
public sealed class EffectiveCredentialCapabilitiesTests : IntegrationTestBase
{
    [Test]
    public async Task GetEffectiveCredentialCapabilities_MixedRoleDecisions_DenyOverridesAllow()
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Deny, roleCount: 2);
        await SeedRoleTypePermission(
            scenario.TenantId,
            scenario.Roles[0].TypeId!.Value,
            TenantModuleFeatureKeys.Identity,
            string.Empty,
            IdentityAuthorizationConstants.View,
            RoleCapabilityPermissionEffect.Allow);
        await SeedRoleTypePermission(
            scenario.TenantId,
            scenario.Roles[1].TypeId!.Value,
            TenantModuleFeatureKeys.Identity,
            string.Empty,
            IdentityAuthorizationConstants.View,
            RoleCapabilityPermissionEffect.Deny);

        var result = await GetEffectiveCapabilities(scenario);

        var capability = GetCapability(
            result,
            TenantModuleFeatureKeys.Identity,
            string.Empty,
            IdentityAuthorizationConstants.View);
        capability.IsAllowed.Should().BeFalse();
        capability.DecisionSource.Should().Be("RoleTypePermissionDeny");
    }

    [Test]
    public async Task GetEffectiveCredentialCapabilities_TenantFeatureDisabled_DeniesAllowedRolePermission()
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Allow);
        await SeedTenantFeature(
            scenario.TenantId,
            TenantModuleFeatureKeys.Wallets,
            TenantModuleFeatureKeys.TransfersSubFeature,
            isEnabled: false);
        await SeedRoleTypePermission(
            scenario.TenantId,
            scenario.Roles[0].TypeId!.Value,
            TenantModuleFeatureKeys.Wallets,
            TenantModuleFeatureKeys.TransfersSubFeature,
            IdentityAuthorizationConstants.View,
            RoleCapabilityPermissionEffect.Allow);

        var result = await GetEffectiveCapabilities(scenario);

        var capability = GetCapability(
            result,
            TenantModuleFeatureKeys.Wallets,
            TenantModuleFeatureKeys.TransfersSubFeature,
            IdentityAuthorizationConstants.View);
        capability.IsAllowed.Should().BeFalse();
        capability.DecisionSource.Should().Be("TenantFeatureDisabled");
    }

    [TestCase(MissingPermissionBehavior.Allow, true, "TenantDefaultAllow")]
    [TestCase(MissingPermissionBehavior.Deny, false, "TenantDefaultDeny")]
    public async Task GetEffectiveCredentialCapabilities_MissingPermission_UsesTenantDefault(
        MissingPermissionBehavior missingBehavior,
        bool expectedAllowed,
        string expectedSource)
    {
        var scenario = await SeedScenario(missingBehavior);

        var result = await GetEffectiveCapabilities(scenario);

        var capability = GetCapability(
            result,
            TenantModuleFeatureKeys.Identity,
            string.Empty,
            IdentityAuthorizationConstants.View);
        capability.IsAllowed.Should().Be(expectedAllowed);
        capability.DecisionSource.Should().Be(expectedSource);
    }

    [Test]
    public async Task GetEffectiveCredentialCapabilities_FullMatrix_ExecutesFixedNumberOfQueries()
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Deny, roleCount: 3);
        foreach (var role in scenario.Roles)
        {
            await SeedRoleTypePermission(
                scenario.TenantId,
                role.TypeId!.Value,
                TenantModuleFeatureKeys.Identity,
                string.Empty,
                IdentityAuthorizationConstants.View,
                RoleCapabilityPermissionEffect.Allow);
        }

        var commandCounter = IntegrationTestFixture.Services.GetRequiredService<DbCommandCounterInterceptor>();
        using var measurement = commandCounter.BeginMeasurement();

        var result = await GetEffectiveCapabilities(scenario);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Capabilities.Should().HaveCount(
            TenantModuleFeatureKeys.All.Count * IdentityAuthorizationConstants.CapabilityKeys.Count);
        measurement.CommandCount.Should().Be(6,
            "the full matrix should be resolved from six batched snapshots, independent of role and capability count");
    }

    [Test]
    public async Task GetEffectiveCredentialCapabilities_ExcessiveActiveRoles_FailsClosed()
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Allow, roleCount: 101);

        var result = await GetEffectiveCapabilities(scenario);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Contain("exceeds supported limits");
    }

    [Test]
    public async Task CheckCredentialCapability_ExcessiveActiveRoles_ReturnsDeniedDecision()
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Allow, roleCount: 101);
        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        IntegrationTestFixture.EstablishTrustedActorContext(
            scope.ServiceProvider,
            scenario.TenantId,
            scenario.CredentialId);
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthorizationService>();

        var result = await service.CheckCredentialCapabilityAsync(new CheckCredentialCapabilityRequest
        {
            CredentialId = scenario.CredentialId,
            ModuleKey = TenantModuleFeatureKeys.Identity,
            SubFeatureKey = string.Empty,
            CapabilityKey = IdentityAuthorizationConstants.View,
            Metadata = new RequestMetadata
            {
                RequestId = Guid.NewGuid()
            }
        });

        result.IsSuccess.Should().BeTrue();
        result.Data!.IsAllowed.Should().BeFalse();
        result.Data.DecisionSource.Should().Be("AuthorizationStateLimitExceeded");
    }

    [Test]
    public async Task GetEffectiveCredentialCapabilities_ExcessivePermissionAssignments_FailsClosed()
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Allow);
        var roleTypeId = scenario.Roles[0].TypeId!.Value;
        await using (var db = CreateDbContext())
        {
            var now = DateTime.UtcNow;
            db.Set<IdentityRoleTypeFeaturePermission>().AddRange(
                Enumerable.Range(0, 1001).Select(index => new IdentityRoleTypeFeaturePermission
                {
                    Id = Guid.NewGuid(),
                    TenantId = scenario.TenantId,
                    RoleTypeId = roleTypeId,
                    ModuleKey = $"OverflowModule{index}",
                    SubFeatureKey = string.Empty,
                    CapabilityKey = IdentityAuthorizationConstants.View,
                    Effect = RoleCapabilityPermissionEffect.Allow,
                    IsEnabled = true,
                    CreatedAt = now,
                    ConcurrencyStamp = Guid.NewGuid()
                }));
            await db.SaveChangesAsync();
        }

        var result = await GetEffectiveCapabilities(scenario);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Contain("exceeds supported limits");
    }

    private static CredentialCapabilityCheckResponse GetCapability(
        XFramework.Core.Patterns.Result<EffectiveCredentialCapabilitiesResponse> result,
        string moduleKey,
        string subFeatureKey,
        string capabilityKey)
    {
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data.Should().NotBeNull();

        return result.Data!.Capabilities.Should().ContainSingle(x =>
                x.ModuleKey == moduleKey &&
                x.SubFeatureKey == subFeatureKey &&
                x.CapabilityKey == capabilityKey)
            .Subject;
    }

    private static async Task<XFramework.Core.Patterns.Result<EffectiveCredentialCapabilitiesResponse>> GetEffectiveCapabilities(
        CapabilityScenario scenario)
    {
        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        IntegrationTestFixture.EstablishTrustedActorContext(
            scope.ServiceProvider,
            scenario.TenantId,
            scenario.CredentialId);
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", scenario.TenantId.ToString()),
                new Claim("credential_id", scenario.CredentialId.ToString())
            ], "IntegrationTest"))
        };

        try
        {
            var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthorizationService>();
            return await service.GetEffectiveCredentialCapabilitiesAsync(
                new GetEffectiveCredentialCapabilitiesRequest
                {
                    CredentialId = scenario.CredentialId,
                    Metadata = new RequestMetadata
                    {
                        RequestId = Guid.NewGuid()
                    }
                });
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    private async Task<CapabilityScenario> SeedScenario(
        MissingPermissionBehavior missingBehavior,
        int roleCount = 1)
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var identityInfoId = Guid.NewGuid();
        var roleGroupId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var roles = new List<IdentityRole>(roleCount);

        await using var db = CreateDbContext();
        db.Set<Tenant>().Add(new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Name = $"Capability tenant {tenantId:N}",
            IsEnabled = true,
            CreatedAt = now
        });
        db.Set<IdentityRoleTypeGroup>().Add(new IdentityRoleTypeGroup
        {
            Id = roleGroupId,
            TenantId = tenantId,
            Name = $"Capability group {tenantId:N}",
            Description = "Effective capability integration test",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = now
        });
        db.Set<IdentityInformation>().Add(new IdentityInformation
        {
            Id = identityInfoId,
            TenantId = tenantId,
            FirstName = "Capability",
            LastName = "Subject",
            IsEnabled = true,
            CreatedAt = now
        });
        db.Set<IdentityCredential>().Add(new IdentityCredential
        {
            Id = credentialId,
            TenantId = tenantId,
            IdentityInfoId = identityInfoId,
            UserName = $"capability_{credentialId:N}",
            IsEnabled = true,
            CreatedAt = now
        });
        db.Set<TenantAuthorizationPolicy>().Add(new TenantAuthorizationPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MissingPermissionBehavior = missingBehavior,
            IsEnabled = true,
            CreatedAt = now
        });

        for (var index = 0; index < roleCount; index++)
        {
            var roleTypeId = Guid.NewGuid();
            db.Set<IdentityRoleType>().Add(new IdentityRoleType
            {
                Id = roleTypeId,
                TenantId = tenantId,
                GroupId = roleGroupId,
                Name = $"Capability role type {index}",
                SystemReferenceId = Guid.NewGuid(),
                IsEnabled = true,
                CreatedAt = now
            });

            var role = new IdentityRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CredentialId = credentialId,
                TypeId = roleTypeId,
                RoleExpiration = now.AddYears(1),
                IsEnabled = true,
                CreatedAt = now
            };
            roles.Add(role);
            db.Set<IdentityRole>().Add(role);
        }

        await db.SaveChangesAsync();
        return new CapabilityScenario(tenantId, credentialId, roles);
    }

    private async Task SeedTenantFeature(
        Guid tenantId,
        string moduleKey,
        string subFeatureKey,
        bool isEnabled)
    {
        await using var db = CreateDbContext();
        db.Set<TenantModuleFeature>().Add(new TenantModuleFeature
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleKey = moduleKey,
            SubFeatureKey = subFeatureKey,
            DisplayName = $"{moduleKey}.{subFeatureKey}",
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedRoleTypePermission(
        Guid tenantId,
        Guid roleTypeId,
        string moduleKey,
        string subFeatureKey,
        string capabilityKey,
        RoleCapabilityPermissionEffect effect)
    {
        await using var db = CreateDbContext();
        db.Set<IdentityRoleTypeFeaturePermission>().Add(new IdentityRoleTypeFeaturePermission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RoleTypeId = roleTypeId,
            ModuleKey = moduleKey,
            SubFeatureKey = subFeatureKey,
            CapabilityKey = capabilityKey,
            Effect = effect,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private sealed record CapabilityScenario(
        Guid TenantId,
        Guid CredentialId,
        IReadOnlyList<IdentityRole> Roles);
}
