using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Inventario.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Api.Services;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class InventarioPlannedFeatureGateTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private Mock<IDataContext> data = null!;
    private Mock<ITenantModuleFeatureService> features = null!;
    private TestTrustedInvocationContextAccessor context = null!;
    private ProductVariationService variations = null!;
    private CancellationTokenSource cancellation = null!;

    [SetUp]
    public void SetUp()
    {
        data = new(MockBehavior.Strict);
        features = new(MockBehavior.Strict);
        context = new(tenantId, capabilities: new HashSet<string> { "inventario.reporting:view" });
        variations = new(data.Object, context, features.Object);
        cancellation = new();
    }

    [TearDown]
    public void TearDown() => cancellation.Dispose();

    [Test]
    public async Task Planning_Disabled_DeniesReadsAndWritesBeforeDataAccess()
    {
        Deny(TenantModuleFeatureKeys.PlanningSubFeature);
        var service = new InventoryPlanningService(data.Object, context, features.Object, variations, new Mock<DbContext>().Object);

        await AssertDenied(service.GetRulesAsync(new(), cancellation.Token));
        await AssertDenied(service.CreateRuleAsync(new(), cancellation.Token));
        await AssertDenied(service.UpdateInventoryReorderRuleAsync(new(), cancellation.Token));
        await AssertDenied(service.GetLowStockAsync(new(), cancellation.Token));
        await AssertDenied(service.GetReorderSuggestionsAsync(new(), cancellation.Token));

        VerifyGate(TenantModuleFeatureKeys.PlanningSubFeature, 5);
    }

    [Test]
    public async Task Purchasing_Disabled_DeniesReadsAndWritesBeforeDataAccess()
    {
        Deny(TenantModuleFeatureKeys.PurchasingSubFeature);
        var stock = new StockPostingService(data.Object, context, features.Object, variations);
        var service = new PurchasingService(data.Object, context, stock, variations, features.Object);

        await AssertDenied(service.GetSuppliersAsync(new(), cancellation.Token));
        await AssertDenied(service.CreateSupplierAsync(new(), cancellation.Token));
        await AssertDenied(service.UpdateSupplierAsync(new(), cancellation.Token));
        await AssertDenied(service.GetPurchaseOrdersAsync(new(), cancellation.Token));
        await AssertDenied(service.GetPurchaseOrderAsync(new(), cancellation.Token));
        await AssertDenied(service.CreatePurchaseOrderAsync(new(), cancellation.Token));
        await AssertDenied(service.SetPurchaseOrderStatusAsync(new(), cancellation.Token));
        await AssertDenied(service.GetReceivingDocumentsAsync(new(), cancellation.Token));
        await AssertDenied(service.ReceiveAsync(new(), cancellation.Token));

        VerifyGate(TenantModuleFeatureKeys.PurchasingSubFeature, 9);
    }

    [Test]
    public async Task Traceability_Disabled_DeniesLotReadsAndWritesBeforeDataAccess()
    {
        Deny(TenantModuleFeatureKeys.TraceabilitySubFeature);
        var service = new InventoryLotService(data.Object, context, features.Object, variations);

        await AssertDenied(service.GetLotsAsync(new(), cancellation.Token));
        await AssertDenied(service.GetLotAsync(new(), cancellation.Token));
        await AssertDenied(service.CreateLotAsync(new(), cancellation.Token));
        await AssertDenied(service.UpdateInventoryLotAsync(new(), cancellation.Token));

        VerifyGate(TenantModuleFeatureKeys.TraceabilitySubFeature, 4);
    }

    [Test]
    public async Task Reporting_Disabled_DeniesOperationalReportsBeforeDataAccess()
    {
        Deny(TenantModuleFeatureKeys.ReportingSubFeature);
        var service = ReportingService();

        await AssertDenied(service.GetLowStockAsync(new(), cancellation.Token));
        await AssertDenied(service.GetNearExpiryAsync(new(), cancellation.Token));
        await AssertDenied(service.GetExpiredAsync(new(), cancellation.Token));
        await AssertDenied(service.GetStockPositionsAsync(new(), cancellation.Token));
        await AssertDenied(service.GetMovementLedgerAsync(new(), cancellation.Token));
        await AssertDenied(service.GetAllocationStatusAsync(new(), cancellation.Token));

        VerifyGate(TenantModuleFeatureKeys.ReportingSubFeature, 6);
    }

    [Test]
    public async Task ExpiryReports_ReportingEnabledButTraceabilityDisabled_DenyBeforeDataAccess()
    {
        features.Setup(x => x.EnsureEnabledAsync(tenantId, TenantModuleFeatureKeys.Inventario,
                TenantModuleFeatureKeys.ReportingSubFeature, cancellation.Token))
            .ReturnsAsync(Result.Success());
        Deny(TenantModuleFeatureKeys.TraceabilitySubFeature);
        var service = ReportingService();

        await AssertDenied(service.GetNearExpiryAsync(new(), cancellation.Token));
        await AssertDenied(service.GetExpiredAsync(new(), cancellation.Token));

        VerifyGate(TenantModuleFeatureKeys.TraceabilitySubFeature, 2);
        VerifyGate(TenantModuleFeatureKeys.ReportingSubFeature, 2);
    }

    [TestCase("/api/inventario/suppliers", TenantModuleFeatureKeys.InventarioPurchasing)]
    [TestCase("/api/inventario/purchase-orders", TenantModuleFeatureKeys.InventarioPurchasing)]
    [TestCase("/api/inventario/receiving", TenantModuleFeatureKeys.InventarioPurchasing)]
    [TestCase("/api/inventario/lots", TenantModuleFeatureKeys.InventarioTraceability)]
    [TestCase("/api/inventario/reorder-rules", TenantModuleFeatureKeys.InventarioPlanning)]
    [TestCase("/api/inventario/planning/reorder-suggestions", TenantModuleFeatureKeys.InventarioPlanning)]
    [TestCase("/api/inventario/reports/stock-positions", TenantModuleFeatureKeys.InventarioReporting)]
    [TestCase("/api/inventario/reports/near-expiry", TenantModuleFeatureKeys.InventarioTraceability)]
    [TestCase("/api/inventario/reports/expired-stock", TenantModuleFeatureKeys.InventarioTraceability)]
    public void Configure_ImplementedWorkflow_MapsToExpectedFeature(string route, string feature)
    {
        var options = new TenantModuleFeatureGateOptions();
        InventarioFeatureGateRoutes.Configure(options);

        var rule = options.Rules.OrderByDescending(x => x.PathPrefix.Length)
            .First(x => x.Matches(route));

        rule.FeatureKey.Should().Be(feature);
    }

    private InventoryReportingService ReportingService() => new(data.Object, context,
        new InventoryPlanningService(data.Object, context, features.Object, variations, new Mock<DbContext>().Object),
        features.Object, new Mock<DbContext>(MockBehavior.Strict).Object);

    [Test]
    public void Fulfillment_RequiresReservationsAndIsRequiredForPosSales_WithoutChangingNewTenantDefault()
    {
        var fulfillment = TenantModuleFeatureKeys.Find(TenantModuleFeatureKeys.InventarioFulfillment)!;
        fulfillment.DefaultEnabled.Should().BeFalse();
        fulfillment.RequiredFeatureKeys.Should().Contain(TenantModuleFeatureKeys.InventarioReservations);
        TenantModuleFeatureKeys.Find(TenantModuleFeatureKeys.PosSales)!.RequiredFeatureKeys
            .Should().Contain(TenantModuleFeatureKeys.InventarioFulfillment);
    }

    private void Deny(string feature) =>
        features.Setup(x => x.EnsureEnabledAsync(tenantId, TenantModuleFeatureKeys.Inventario,
                feature, cancellation.Token))
            .ReturnsAsync(Result.Forbidden("Feature disabled"));

    private void VerifyGate(string feature, int calls)
    {
        features.Verify(x => x.EnsureEnabledAsync(tenantId, TenantModuleFeatureKeys.Inventario,
            feature, cancellation.Token), Times.Exactly(calls));
        data.VerifyNoOtherCalls();
    }

    private static async Task AssertDenied<T>(Task<Result<T>> operation)
    {
        var result = await operation;
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Message.Should().Be("Feature disabled");
    }
}
