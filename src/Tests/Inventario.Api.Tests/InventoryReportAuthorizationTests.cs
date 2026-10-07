using FluentAssertions;
using Moq;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class InventoryReportAuthorizationTests
{
    [TestCase(false, false, 401)]
    [TestCase(true, false, 403)]
    public async Task GetSnapshotAsync_WithoutAuthenticatedReportingActor_DeniesBeforeQuery(bool actor, bool capability, int status)
    {
        var tenant = Guid.NewGuid();
        var result = await Service(actor ? tenant : null, capability).GetSnapshotAsync(Request(tenant));
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(status);
    }

    [Test]
    public async Task GetSnapshotAsync_ForgedTenant_DeniesBeforeQuery()
    {
        var result = await Service(Guid.NewGuid(), true).GetSnapshotAsync(Request(Guid.NewGuid()));
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task GetSnapshotAsync_MissingExplicitTenant_DeniesBeforeQuery()
    {
        var tenant = Guid.NewGuid();
        var request = Request(tenant) with { Metadata = new() };
        (await Service(tenant, true).GetSnapshotAsync(request)).StatusCode.Should().Be(403);
    }

    [TestCase(-1, 30)]
    [TestCase(400, 30)]
    [TestCase(30, 0)]
    [TestCase(30, 366)]
    public async Task GetSnapshotAsync_InvalidDatesOrExpiry_DeniesBeforeQuery(int days, int expiry)
    {
        var tenant = Guid.NewGuid();
        var request = Request(tenant) with { FromUtc = DateTime.UtcNow.AddDays(-days), DaysAhead = expiry };
        (await Service(tenant, true).GetSnapshotAsync(request)).StatusCode.Should().Be(400);
    }

    [Test]
    public async Task GetSnapshotAsync_ReportingFeatureDisabled_DeniesBeforeQuery()
    {
        var tenant = Guid.NewGuid();
        (await Service(tenant, true, false).GetSnapshotAsync(Request(tenant))).StatusCode.Should().Be(403);
    }

    [Test]
    public void ReportEndpoints_RequireReportingViewAndManagedTenantOnBothTransports()
    {
        var endpoints = typeof(global::Inventario.Api.Features.Reports.Snapshot.InventoryReportSnapshotEndpoint)
            .Assembly.GetTypes().Where(x => x.Namespace?.StartsWith("Inventario.Api.Features.Reports.") == true)
            .SelectMany(x => x.GetMethods()).Where(x => x.Name == "Handle").ToList();
        endpoints.Should().HaveCount(7);
        foreach (var method in endpoints)
        {
            var bolt = method.GetCustomAttributes(typeof(BoltHandlerAttribute), false).Cast<BoltHandlerAttribute>().Single();
            var rest = method.GetCustomAttributes(typeof(MapEndpointAttribute), false).Cast<MapEndpointAttribute>().Single();
            bolt.RequiredActorCapabilities.Should().Contain("inventario.reporting:view");
            rest.RequiredActorCapabilities.Should().Contain("inventario.reporting:view");
            bolt.TenantAccessMode.Should().Be(TenantAccessMode.DelegatedTenant);
            rest.TenantAccessMode.Should().Be(TenantAccessMode.DelegatedTenant);
            bolt.AllowAnonymous.Should().BeFalse();
            rest.AllowAnonymous.Should().BeFalse();
            rest.RequireAuthorization.Should().BeTrue();
        }
    }

    private static GetInventoryReportSnapshotRequest Request(Guid tenant) => new()
    {
        Metadata = new() { RequestedTenantId = tenant }, FromUtc = DateTime.UtcNow.AddDays(-30), ToUtc = DateTime.UtcNow
    };

    private static InventoryReportingService Service(Guid? tenant, bool capability, bool enabled = true)
    {
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        var context = new TestTrustedInvocationContextAccessor(tenant,
            capabilities: capability ? new HashSet<string> { "inventario.reporting:view" } : []);
        var features = new Mock<ITenantModuleFeatureService>();
        features.Setup(x => x.EnsureEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(enabled ? Result.Success() : Result.Forbidden("disabled"));
        var variations = new ProductVariationService(data.Object, context, features.Object);
        var planning = new InventoryPlanningService(data.Object, context, features.Object, variations);
        return new(data.Object, context, planning, features.Object);
    }
}
