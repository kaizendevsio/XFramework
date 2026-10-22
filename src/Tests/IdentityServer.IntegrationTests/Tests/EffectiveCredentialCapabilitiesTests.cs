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

    [TestCase(TenantModuleFeatureKeys.Wallets, TenantModuleFeatureKeys.TransfersSubFeature)]
    [TestCase(TenantModuleFeatureKeys.Pos, "")]
    [TestCase(TenantModuleFeatureKeys.Pos, TenantModuleFeatureKeys.SalesSubFeature)]
    [TestCase(TenantModuleFeatureKeys.Pos, TenantModuleFeatureKeys.ReturnsSubFeature)]
    [TestCase(TenantModuleFeatureKeys.Inventario, TenantModuleFeatureKeys.InventarioReportingSubFeature)]
    [TestCase(TenantModuleFeatureKeys.Inventario, TenantModuleFeatureKeys.ReservationsSubFeature)]
    public async Task GetEffectiveCredentialCapabilities_TenantFeatureDisabled_DeniesAllowedRolePermission(
        string moduleKey, string subFeatureKey)
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Allow);
        await SeedTenantFeature(
            scenario.TenantId,
            moduleKey,
            subFeatureKey,
            isEnabled: false);
        await SeedRoleTypePermission(
            scenario.TenantId,
            scenario.Roles[0].TypeId!.Value,
            moduleKey,
            subFeatureKey,
            IdentityAuthorizationConstants.View,
            RoleCapabilityPermissionEffect.Allow);

        var result = await GetEffectiveCapabilities(scenario);

        var capability = GetCapability(
            result,
            moduleKey,
            subFeatureKey,
            IdentityAuthorizationConstants.View);
        capability.IsAllowed.Should().BeFalse();
        capability.DecisionSource.Should().Be("TenantFeatureDisabled");
    }

    [TestCase(IdentityAuthorizationConstants.View, false)]
    [TestCase(IdentityAuthorizationConstants.Manage, false)]
    [TestCase(IdentityAuthorizationConstants.View, true)]
    [TestCase(IdentityAuthorizationConstants.Manage, true)]
    public async Task PosReportingCapabilities_ExplicitGrant_DoesNotDependOnHomeTenantFeature(
        string grantedCapability, bool homeFeatureEnabled)
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Deny);
        await SeedTenantFeature(scenario.TenantId, TenantModuleFeatureKeys.Pos,
            TenantModuleFeatureKeys.PosReportingSubFeature, homeFeatureEnabled);
        await SeedRoleTypePermission(scenario.TenantId, scenario.Roles[0].TypeId!.Value,
            TenantModuleFeatureKeys.Pos, TenantModuleFeatureKeys.PosReportingSubFeature,
            grantedCapability, RoleCapabilityPermissionEffect.Allow);

        await AssertPosReportingCapability(scenario, true, "RoleTypePermissionAllow");
    }

    [TestCase(false, IdentityAuthorizationConstants.View)]
    [TestCase(false, IdentityAuthorizationConstants.Manage)]
    [TestCase(true, IdentityAuthorizationConstants.View)]
    [TestCase(true, IdentityAuthorizationConstants.Manage)]
    public async Task PosReportingCapabilities_HomeFeatureDisabled_DenyStillOverridesAnotherRoleGrant(
        bool credentialOverride, string deniedCapability)
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Allow, roleCount: 2);
        await SeedTenantFeature(scenario.TenantId, TenantModuleFeatureKeys.Pos,
            TenantModuleFeatureKeys.PosReportingSubFeature, isEnabled: false);
        foreach (var role in scenario.Roles)
        {
            await SeedRoleTypePermission(scenario.TenantId, role.TypeId!.Value,
                TenantModuleFeatureKeys.Pos, TenantModuleFeatureKeys.PosReportingSubFeature,
                IdentityAuthorizationConstants.View, RoleCapabilityPermissionEffect.Allow);
        }

        await using (var db = CreateDbContext())
        {
            if (credentialOverride)
            {
                db.Set<IdentityRoleFeaturePermissionOverride>().Add(new IdentityRoleFeaturePermissionOverride
                {
                    Id = Guid.NewGuid(), TenantId = scenario.TenantId, IdentityRoleId = scenario.Roles[1].Id,
                    ModuleKey = TenantModuleFeatureKeys.Pos, SubFeatureKey = TenantModuleFeatureKeys.PosReportingSubFeature,
                    CapabilityKey = deniedCapability, Effect = RoleCapabilityPermissionEffect.Deny,
                    IsEnabled = true, CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                // Replace the second role's grant to exercise deny precedence across active roles.
                var permission = await db.Set<IdentityRoleTypeFeaturePermission>().IgnoreQueryFilters().AsTracking()
                    .SingleAsync(x => x.TenantId == scenario.TenantId && x.RoleTypeId == scenario.Roles[1].TypeId);
                permission.CapabilityKey = deniedCapability;
                permission.Effect = RoleCapabilityPermissionEffect.Deny;
            }
            await db.SaveChangesAsync();
        }

        await AssertPosReportingCapability(scenario, false,
            credentialOverride ? "CredentialRoleOverrideDeny" : "RoleTypePermissionDeny");
    }

    [TestCase(MissingPermissionBehavior.Allow, false)]
    [TestCase(MissingPermissionBehavior.Deny, false)]
    [TestCase(MissingPermissionBehavior.Allow, true)]
    [TestCase(MissingPermissionBehavior.Deny, true)]
    public async Task PosReportingCapabilities_NoExplicitGrant_DeniesRegardlessOfTenantDefault(
        MissingPermissionBehavior missingBehavior, bool homeFeatureEnabled)
    {
        var scenario = await SeedScenario(missingBehavior);
        await SeedTenantFeature(scenario.TenantId, TenantModuleFeatureKeys.Pos,
            TenantModuleFeatureKeys.PosReportingSubFeature, homeFeatureEnabled);
        await SeedTenantFeature(scenario.TenantId, TenantModuleFeatureKeys.Identity,
            TenantModuleFeatureKeys.TenantsSubFeature, isEnabled: true);
        await SeedRoleTypePermission(scenario.TenantId, scenario.Roles[0].TypeId!.Value,
            TenantModuleFeatureKeys.Identity, TenantModuleFeatureKeys.TenantsSubFeature,
            IdentityAuthorizationConstants.Manage, RoleCapabilityPermissionEffect.Allow);

        await AssertPosReportingCapability(scenario, false, "TenantDefaultDeny");
    }

    [Test]
    public async Task PosReportingCapabilities_NoActiveRole_DeniesEvenWithPermissiveTenantDefault()
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Allow, roleCount: 0);

        await AssertPosReportingCapability(scenario, false, "NoActiveRole");
    }

    [TestCase(IdentityAuthorizationConstants.View, true)]
    [TestCase(IdentityAuthorizationConstants.Manage, true)]
    [TestCase(IdentityAuthorizationConstants.View, false)]
    [TestCase(IdentityAuthorizationConstants.Manage, false)]
    public async Task InventarioPlanningCapabilities_HomeDisabled_PreservesExplicitGrantAndIndependentTargetGate(
        string grantedCapability, bool targetEnabled)
    {
        var home = await SeedScenario(MissingPermissionBehavior.Deny);
        var target = await SeedScenario(MissingPermissionBehavior.Deny, roleCount: 0);
        await SeedTenantFeature(home.TenantId, TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.PlanningSubFeature, isEnabled: false);
        await SeedTenantFeature(target.TenantId, TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.PlanningSubFeature, isEnabled: targetEnabled);
        await SeedRoleTypePermission(home.TenantId, home.Roles[0].TypeId!.Value,
            TenantModuleFeatureKeys.Inventario, TenantModuleFeatureKeys.PlanningSubFeature,
            grantedCapability, RoleCapabilityPermissionEffect.Allow);

        await AssertCapability(home, TenantModuleFeatureKeys.Inventario, TenantModuleFeatureKeys.PlanningSubFeature,
            true, "RoleTypePermissionAllow");

        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        var features = scope.ServiceProvider.GetRequiredService<XFramework.Core.Services.FeatureGates.ITenantModuleFeatureService>();
        var homeGate = await features.EnsureEnabledAsync(home.TenantId, TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.PlanningSubFeature);
        var targetGate = await features.EnsureEnabledAsync(target.TenantId, TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.PlanningSubFeature);
        homeGate.IsSuccess.Should().BeFalse();
        homeGate.StatusCode.Should().Be(403);
        targetGate.IsSuccess.Should().Be(targetEnabled);
        targetGate.StatusCode.Should().Be(targetEnabled ? 200 : 403);
    }

    [TestCase(false, IdentityAuthorizationConstants.View)]
    [TestCase(false, IdentityAuthorizationConstants.Manage)]
    [TestCase(true, IdentityAuthorizationConstants.View)]
    [TestCase(true, IdentityAuthorizationConstants.Manage)]
    public async Task InventarioPlanningCapabilities_HomeDisabled_DenyOverridesAnotherRoleGrant(
        bool credentialOverride, string deniedCapability)
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Allow, roleCount: 2);
        await SeedTenantFeature(scenario.TenantId, TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.PlanningSubFeature, isEnabled: false);
        await SeedRoleTypePermission(scenario.TenantId, scenario.Roles[0].TypeId!.Value,
            TenantModuleFeatureKeys.Inventario, TenantModuleFeatureKeys.PlanningSubFeature,
            IdentityAuthorizationConstants.View, RoleCapabilityPermissionEffect.Allow);
        await SeedRoleTypePermission(scenario.TenantId, scenario.Roles[1].TypeId!.Value,
            TenantModuleFeatureKeys.Inventario, TenantModuleFeatureKeys.PlanningSubFeature,
            deniedCapability, credentialOverride ? RoleCapabilityPermissionEffect.Allow : RoleCapabilityPermissionEffect.Deny);
        if (credentialOverride)
        {
            await using var db = CreateDbContext();
            db.Set<IdentityRoleFeaturePermissionOverride>().Add(new IdentityRoleFeaturePermissionOverride
            {
                Id = Guid.NewGuid(), TenantId = scenario.TenantId, IdentityRoleId = scenario.Roles[1].Id,
                ModuleKey = TenantModuleFeatureKeys.Inventario, SubFeatureKey = TenantModuleFeatureKeys.PlanningSubFeature,
                CapabilityKey = deniedCapability, Effect = RoleCapabilityPermissionEffect.Deny,
                IsEnabled = true, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await AssertCapability(scenario, TenantModuleFeatureKeys.Inventario, TenantModuleFeatureKeys.PlanningSubFeature,
            false, credentialOverride ? "CredentialRoleOverrideDeny" : "RoleTypePermissionDeny");
    }

    [TestCase(0, "NoActiveRole")]
    [TestCase(1, "TenantDefaultDeny")]
    public async Task InventarioPlanningCapabilities_NoExplicitGrant_PermissiveDefaultDoesNotGrantAccess(
        int roleCount, string expectedSource)
    {
        var scenario = await SeedScenario(MissingPermissionBehavior.Allow, roleCount);
        await SeedTenantFeature(scenario.TenantId, TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.PlanningSubFeature, isEnabled: false);
        if (roleCount > 0)
        {
            await SeedRoleTypePermission(scenario.TenantId, scenario.Roles[0].TypeId!.Value,
                TenantModuleFeatureKeys.Identity, TenantModuleFeatureKeys.TenantsSubFeature,
                IdentityAuthorizationConstants.Manage, RoleCapabilityPermissionEffect.Allow);
        }

        await AssertCapability(scenario, TenantModuleFeatureKeys.Inventario, TenantModuleFeatureKeys.PlanningSubFeature,
            false, expectedSource);
    }

    private static Task AssertPosReportingCapability(CapabilityScenario scenario, bool expectedAllowed, string expectedSource) =>
        AssertCapability(scenario, TenantModuleFeatureKeys.Pos, TenantModuleFeatureKeys.PosReportingSubFeature,
            expectedAllowed, expectedSource);

    private static async Task AssertCapability(
        CapabilityScenario scenario, string moduleKey, string subFeatureKey, bool expectedAllowed, string expectedSource)
    {
        var effective = GetCapability(await GetEffectiveCapabilities(scenario),
            moduleKey, subFeatureKey,
            IdentityAuthorizationConstants.View);
        effective.IsAllowed.Should().Be(expectedAllowed);
        effective.DecisionSource.Should().Be(expectedSource);

        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        IntegrationTestFixture.EstablishTrustedActorContext(scope.ServiceProvider, scenario.TenantId, scenario.CredentialId);
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthorizationService>();
        var trusted = GetCapability(await service.GetTrustedEffectiveCredentialCapabilitiesAsync(scenario.TenantId, scenario.CredentialId),
            moduleKey, subFeatureKey,
            IdentityAuthorizationConstants.View);
        trusted.Should().BeEquivalentTo(effective, "actor-token capabilities must match the public capability matrix");
        var check = await service.CheckCredentialCapabilityAsync(new CheckCredentialCapabilityRequest
        {
            CredentialId = scenario.CredentialId, ModuleKey = moduleKey,
            SubFeatureKey = subFeatureKey,
            CapabilityKey = IdentityAuthorizationConstants.View,
            Metadata = new RequestMetadata { RequestId = Guid.NewGuid() }
        });
        check.IsSuccess.Should().BeTrue(check.Message);
        check.StatusCode.Should().Be(200);
        check.Data.Should().BeEquivalentTo(effective, "single-capability checks must use the same policy as actor-token issuance");
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
