using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Portal.Features.Identity.Services;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class TenantModuleFeatureEnablementPlannerTests
{
    [Test]
    public void Create_RequiredDependencies_EnablesTransitiveFeaturesAndParentsOnly()
    {
        var plan = TenantModuleFeatureEnablementPlanner.Create(BuiltIns(), Enabled(), [TenantModuleFeatureKeys.PosReturns], []);
        plan.Errors.Should().BeEmpty();
        plan.EnableKeys.Should().Contain(["pos.returns", "pos.sales", "pos.registers", "pos", "identity", "identity.credentials",
            "wallets", "inventario", "inventario.warehousing", "inventario.catalog", "inventario.reservations", "inventario.movements"]);
        plan.EnableKeys.Should().OnlyHaveUniqueItems();
        plan.EnableKeys.Should().NotContain("inventario.purchasing").And.NotContain("pos.reporting");
    }

    [Test]
    public void Create_AlreadyEnabledTargetWithMissingDependencies_PlansRepairWithoutRepeatingEnabledKeys()
    {
        var plan = TenantModuleFeatureEnablementPlanner.Create(BuiltIns(), Enabled("pos", "pos.registers", "identity", "identity.credentials", "wallets", "inventario"),
            ["pos.registers"], []);
        plan.EnableKeys.Should().Equal("inventario.warehousing");
    }

    [Test]
    public void Create_OptionalDependency_DoesNotEnableIt()
    {
        var definition = Feature("test", "") with
        { Dependencies = [new() { Kind = BoltDependencyKind.TenantFeature, Key = "optional", Required = false }] };
        var plan = TenantModuleFeatureEnablementPlanner.Create([definition, Feature("optional", "")], Enabled(), ["test"], []);
        plan.Errors.Should().BeEmpty();
        plan.EnableKeys.Should().Equal("test");
    }

    [Test]
    public void Create_UnknownRequiredDependency_BlocksTheWholePlan()
    {
        var plan = TenantModuleFeatureEnablementPlanner.Create([Feature("test", "", "missing")], Enabled(), ["test"], []);
        plan.Errors.Should().ContainSingle().Which.Should().Contain("missing");
    }

    [Test]
    public void Create_OfflineServiceDependency_BlocksTheWholePlan()
    {
        var unavailable = Feature("test", "") with { UnavailableRequiredDependencies = ["Service is offline."] };
        var plan = TenantModuleFeatureEnablementPlanner.Create([unavailable], Enabled(), ["test"], []);
        plan.Errors.Should().Equal("Service is offline.");
    }

    [Test]
    public void Create_CyclicDependencies_TerminatesWithUniqueKeys()
    {
        var plan = TenantModuleFeatureEnablementPlanner.Create([Feature("one", "", "two"), Feature("two", "", "one")], Enabled(), ["one"], []);
        plan.Errors.Should().BeEmpty();
        plan.EnableKeys.Should().BeEquivalentTo(["one", "two"]);
    }

    [Test]
    public void Create_DisablingRequiredFeatureWithEnabledDependent_IsRejected()
    {
        var plan = TenantModuleFeatureEnablementPlanner.Create(BuiltIns(), Enabled("pos.registers", "inventario.warehousing"), [], ["inventario.warehousing"]);
        plan.Errors.Should().ContainSingle().Which.Should().Contain("POS Registers").And.Contain("Warehousing");
    }

    [Test]
    public void Create_DisablingDependentAndDependencyTogether_IsAllowed()
    {
        var plan = TenantModuleFeatureEnablementPlanner.Create(BuiltIns(), Enabled("pos.registers", "inventario.warehousing"), [], ["pos.registers", "inventario.warehousing"]);
        plan.Errors.Should().BeEmpty();
    }

    [Test]
    public void Create_DisablingParentWithEnabledChild_IsRejected()
    {
        var plan = TenantModuleFeatureEnablementPlanner.Create(BuiltIns(), Enabled("inventario", "inventario.catalog"), [], ["inventario"]);
        plan.Errors.Should().ContainSingle().Which.Should().Contain("Catalog");
    }

    [Test]
    public void Create_EnabledAndDisabledDependencyInSamePlan_IsRejected()
    {
        var plan = TenantModuleFeatureEnablementPlanner.Create(BuiltIns(), Enabled(), ["pos.registers"], ["wallets"]);
        plan.Errors.Should().NotBeEmpty();
    }

    private static HashSet<string> Enabled(params string[] keys) => new(keys, StringComparer.OrdinalIgnoreCase);
    private static List<ResolvedTenantModuleFeatureDefinition> BuiltIns() => TenantModuleFeatureKeys.All.Select(definition =>
        new ResolvedTenantModuleFeatureDefinition(definition, [], [])
        {
            Dependencies = definition.RequiredFeatureKeys.Select(key => new BoltDependencyRequirement
            { Kind = BoltDependencyKind.TenantFeature, Key = key }).ToList()
        }).ToList();
    private static ResolvedTenantModuleFeatureDefinition Feature(string module, string child, params string[] dependencies) =>
        new(new(module, child, module, "", "box"), [], [])
        {
            Dependencies = dependencies.Select(key => new BoltDependencyRequirement
            { Kind = BoltDependencyKind.TenantFeature, Key = key }).ToList()
        };
}
