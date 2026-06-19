using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace XFramework.Core.Tests.Services.FeatureGates;

[TestFixture]
public sealed class TenantModuleFeatureCatalogTests
{
    [Test]
    public void Definitions_BuiltInProvider_ReturnsTenantModuleFeatureKeysAll()
    {
        var provider = new BuiltInTenantModuleFeatureDefinitionProvider();

        provider.Definitions.Should().Equal(TenantModuleFeatureKeys.All);
    }

    [Test]
    public void All_ExternalProviderRegistered_IncludesExternalDefinitions()
    {
        var catalog = new TenantModuleFeatureCatalog(
        [
            new BuiltInTenantModuleFeatureDefinitionProvider(),
            new TestTenantModuleFeatureDefinitionProvider(
                new TenantModuleFeatureDefinition(
                    "juan_barangay",
                    "residents",
                    "Residents",
                    "Resident registry and household records.",
                    "users"))
        ]);

        catalog.All.Should().Contain(definition =>
            definition.Key == "juan_barangay.residents" &&
            definition.DisplayName == "Residents");
    }

    [Test]
    public void Find_CombinedAndSplitKeys_ReturnsMatchingDefinition()
    {
        var catalog = new TenantModuleFeatureCatalog(
        [
            new TestTenantModuleFeatureDefinitionProvider(
                new TenantModuleFeatureDefinition(
                    "juan_barangay",
                    "health_services",
                    "Health Services",
                    "Health service case management.",
                    "heart-pulse"))
        ]);

        var combinedResult = catalog.Find("Juan_Barangay.Health_Services");
        var splitResult = catalog.Find("juan_barangay", "health_services");

        combinedResult.Should().NotBeNull();
        splitResult.Should().BeSameAs(combinedResult);
    }

    [Test]
    public void All_DuplicateNormalizedKeys_KeepsFirstDefinition()
    {
        var catalog = new TenantModuleFeatureCatalog(
        [
            new TestTenantModuleFeatureDefinitionProvider(
                new TenantModuleFeatureDefinition(
                    "Juan_Barangay",
                    "Residents",
                    "First Residents",
                    "First definition wins.",
                    "users")),
            new TestTenantModuleFeatureDefinitionProvider(
                new TenantModuleFeatureDefinition(
                    "juan_barangay.residents",
                    string.Empty,
                    "Second Residents",
                    "Duplicate definition should be ignored.",
                    "user-x"))
        ]);

        var matchingDefinitions = catalog.All
            .Where(definition => definition.Key == "juan_barangay.residents")
            .ToList();

        matchingDefinitions.Should().ContainSingle();
        matchingDefinitions[0].DisplayName.Should().Be("First Residents");
        matchingDefinitions[0].ModuleKey.Should().Be("juan_barangay");
        matchingDefinitions[0].SubFeatureKey.Should().Be("residents");
    }

    [Test]
    public void AddTenantModuleFeatureDefinitions_ConfigurationContainsDefinitions_RegistersConfigDefinitions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantModuleFeatures:Definitions:0:Key"] = "juan_barangay.residents",
                ["TenantModuleFeatures:Definitions:0:DisplayName"] = "Residents",
                ["TenantModuleFeatures:Definitions:0:Description"] = "Resident registry and household records.",
                ["TenantModuleFeatures:Definitions:0:IconName"] = "users",
                ["TenantModuleFeatures:Definitions:1:ModuleKey"] = "juan_barangay",
                ["TenantModuleFeatures:Definitions:1:SubFeatureKey"] = "health_services",
                ["TenantModuleFeatures:Definitions:1:DisplayName"] = "Health Services",
                ["TenantModuleFeatures:Definitions:1:DefaultEnabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTenantModuleFeatureDefinitions(configuration);

        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<ITenantModuleFeatureCatalog>();

        catalog.All.Should().Contain(definition =>
            definition.Key == "juan_barangay.residents" &&
            definition.DisplayName == "Residents" &&
            definition.DefaultEnabled);
        catalog.All.Should().Contain(definition =>
            definition.Key == "juan_barangay.health_services" &&
            definition.DisplayName == "Health Services" &&
            !definition.DefaultEnabled);
    }

    private sealed class TestTenantModuleFeatureDefinitionProvider(
        params TenantModuleFeatureDefinition[] definitions) : ITenantModuleFeatureDefinitionProvider
    {
        public IReadOnlyList<TenantModuleFeatureDefinition> Definitions { get; } = definitions;
    }
}
