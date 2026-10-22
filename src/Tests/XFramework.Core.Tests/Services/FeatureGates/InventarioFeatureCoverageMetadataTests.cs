using System;
using System.Reflection;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using NUnit.Framework;
using XFramework.Portal.Features.Identity.Pages;

namespace XFramework.Core.Tests.Services.FeatureGates;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class InventarioFeatureCoverageMetadataTests
{
    [TestCase(TenantModuleFeatureKeys.InventarioPurchasing, "API gated", "active")]
    [TestCase(TenantModuleFeatureKeys.InventarioTraceability, "API gated", "active")]
    [TestCase(TenantModuleFeatureKeys.InventarioPlanning, "API gated", "active")]
    [TestCase(TenantModuleFeatureKeys.InventarioReporting, "API gated", "active")]
    [TestCase(TenantModuleFeatureKeys.InventarioNegativeStock, "API gated", "active")]
    [TestCase(TenantModuleFeatureKeys.InventarioFulfillment, "API gated", "active")]
    public void Coverage_ReflectsEnforcedFeatureNotTenantToggle(string key, string text, string status)
    {
        var rowType = typeof(TenantDetail).GetNestedType("ModuleFeatureRow", BindingFlags.NonPublic)!;
        var definition = TenantModuleFeatureKeys.Find(key)!;
        foreach (var enabled in new[] { false, true })
        {
            var row = Activator.CreateInstance(rowType,
                [Guid.NewGuid(), key, definition.ModuleKey, definition.SubFeatureKey,
                    definition.DisplayName, definition.Description, definition.IconName, enabled, false,
                    Array.Empty<string>(), Array.Empty<string>()]);

            Invoke("GetModuleFeatureCoverageText", row!).Should().Be(text);
            Invoke("GetModuleFeatureCoverageStatus", row!).Should().Be(status);
        }
    }

    [TestCase(TenantModuleFeatureKeys.InventarioFulfillment, "Pick, pack, ship, and order fulfillment operations.")]
    [TestCase(TenantModuleFeatureKeys.InventarioTraceability, "Lot, serial, batch, and inventory lineage tracking.")]
    [TestCase(TenantModuleFeatureKeys.InventarioPlanning, "Demand planning, reorder planning, and replenishment forecasting.")]
    [TestCase(TenantModuleFeatureKeys.InventarioReporting, "Inventory analytics, valuation, audit, and operational reports.")]
    public void Description_LegacyBuiltInUsesDeliveredScope_CustomTextIsPreserved(string key, string legacy)
    {
        var definition = TenantModuleFeatureKeys.Find(key)!;
        var method = typeof(TenantDetail).GetMethod("GetModuleFeatureDescription", BindingFlags.NonPublic | BindingFlags.Static)!;
        method.Invoke(null, [definition, legacy]).Should().Be(definition.Description);
        method.Invoke(null, [definition, null]).Should().Be(definition.Description);
        method.Invoke(null, [definition, "Our custom workflow"]).Should().Be("Our custom workflow");
    }

    private static string Invoke(string method, object row) =>
        (string)typeof(TenantDetail).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [row])!;
}
