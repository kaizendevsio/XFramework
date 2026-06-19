using System;
using System.Collections.Generic;
using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using ControlPanel.Server.Services;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using NUnit.Framework;

namespace XFramework.Core.Tests.Services.FeatureGates;

[TestFixture]
public sealed class TenantModuleFeatureDefinitionResolverTests
{
    [Test]
    public void CreateResolvedDefinition_RequiredTenantFeatureDependencyMissing_BlocksDefinitionAndDisablesDefault()
    {
        var module = new BoltModuleRegistryItem
        {
            ModuleKey = "juan_barangay",
            DisplayName = "Juan Barangay",
            ServiceName = "JuanBarangay",
            Status = BoltRegistryStatus.Online
        };
        var feature = new BoltTenantModuleFeatureRegistryItem
        {
            Key = "juan_barangay.health_services",
            ModuleKey = "juan_barangay",
            SubFeatureKey = "health_services",
            DisplayName = "Health Services",
            Description = "Health service case management.",
            IconName = "heart-pulse",
            DefaultEnabled = true,
            Status = BoltRegistryStatus.Online,
            Dependencies =
            [
                new BoltDependencyRequirement
                {
                    Kind = BoltDependencyKind.TenantFeature,
                    Key = "juan_barangay.residents",
                    DisplayName = "Residents",
                    Required = true
                }
            ]
        };

        var resolved = TenantModuleFeatureDefinitionResolver.CreateResolvedDefinition(
            module,
            feature,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        resolved.IsBlocked.Should().BeTrue();
        resolved.Definition.DefaultEnabled.Should().BeFalse();
        resolved.MissingRequiredDependencies.Should().ContainSingle()
            .Which.Should().Contain("Residents");
    }

    [Test]
    public void CreateResolvedDefinition_RequiredTenantFeatureDependencyEnabled_AllowsDefinitionDefault()
    {
        var module = new BoltModuleRegistryItem
        {
            ModuleKey = "juan_barangay",
            DisplayName = "Juan Barangay",
            ServiceName = "JuanBarangay",
            Status = BoltRegistryStatus.Online
        };
        var feature = new BoltTenantModuleFeatureRegistryItem
        {
            Key = "juan_barangay.health_services",
            ModuleKey = "juan_barangay",
            SubFeatureKey = "health_services",
            DisplayName = "Health Services",
            Description = "Health service case management.",
            IconName = "heart-pulse",
            DefaultEnabled = true,
            Status = BoltRegistryStatus.Online,
            Dependencies =
            [
                new BoltDependencyRequirement
                {
                    Kind = BoltDependencyKind.TenantFeature,
                    Key = "juan_barangay.residents",
                    DisplayName = "Residents",
                    Required = true
                }
            ]
        };

        var resolved = TenantModuleFeatureDefinitionResolver.CreateResolvedDefinition(
            module,
            feature,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "juan_barangay.residents" });

        resolved.IsBlocked.Should().BeFalse();
        resolved.Definition.DefaultEnabled.Should().BeTrue();
        resolved.MissingRequiredDependencies.Should().BeEmpty();
    }

    [Test]
    public void MergeResolvedDefinition_DuplicateDiscoveredKey_PreservesFirstDefinitionAndMergesDependencyState()
    {
        var builtIn = new ResolvedTenantModuleFeatureDefinition(
            new TenantModuleFeatureDefinition(
                "wallets",
                string.Empty,
                "Wallets",
                "Built-in wallet module.",
                "wallet"),
            [],
            ["Optional accounting service is not available."]);
        var discovered = new ResolvedTenantModuleFeatureDefinition(
            new TenantModuleFeatureDefinition(
                "wallets",
                string.Empty,
                "Discovered Wallets",
                "Discovered wallet module.",
                "credit-card"),
            ["Service Wallets is offline."],
            ["Optional accounting service is not available."]);

        var merged = TenantModuleFeatureDefinitionResolver.MergeResolvedDefinition(builtIn, discovered);

        merged.Definition.DisplayName.Should().Be("Wallets");
        merged.Definition.Description.Should().Be("Built-in wallet module.");
        merged.MissingRequiredDependencies.Should().ContainSingle()
            .Which.Should().Be("Service Wallets is offline.");
        merged.MissingOptionalDependencies.Should().ContainSingle()
            .Which.Should().Be("Optional accounting service is not available.");
        merged.IsBlocked.Should().BeTrue();
    }
}
