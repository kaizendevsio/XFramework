using FluentAssertions;
using Microsoft.AspNetCore.Components;
using XFramework.Portal.Features.Inventario.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class InventoryReportScopeTests
{
    [Test]
    public void ShareUri_RoundTripsTenantFiltersAndDatesWithoutChangingSelectedTenant()
    {
        var tenant = Guid.NewGuid();
        var selectedTenant = Guid.NewGuid();
        var scope = InventoryReportScope.Default(tenant) with
        {
            ProductId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), LocationId = Guid.NewGuid(), DaysAhead = 17
        };
        var navigation = new TestNavigation("https://portal.example/inventario/reports?token=do-not-share");
        var uri = scope.ShareUri(navigation);
        InventoryReportScope.Parse(uri, selectedTenant).Should().Be(scope);
        uri.Should().NotContain("token").And.NotContain("bearer");
        scope.Request().Metadata.RequestedTenantId.Should().Be(tenant);
        navigation.Uri.Should().Contain("do-not-share", "composing a share link must not navigate or change context");
    }

    [TestCase("?product=invalid")]
    [TestCase("?tenant=invalid")]
    [TestCase("?tenant={tenant}&product=invalid")]
    [TestCase("?tenant={tenant}&warehouse=")]
    [TestCase("?tenant={tenant}&from=2026-10-03&to=2026-10-01")]
    [TestCase("?tenant={tenant}&expiry=0")]
    [TestCase("?tenant={tenant}&from=bad")]
    public void Parse_MalformedSharedScope_FailsClosed(string query)
    {
        var tenant = Guid.NewGuid();
        var act = () => InventoryReportScope.Parse("https://portal.example/inventario/reports" +
            query.Replace("{tenant}", tenant.ToString()), tenant);
        act.Should().Throw<FormatException>();
    }

    [Test]
    public void Export_UsesCapturedAsOfTotalsAndDetailRowsWithCharts()
    {
        var now = DateTime.SpecifyKind(new DateTime(2026, 10, 7, 12, 0, 0), DateTimeKind.Utc);
        var snapshot = new InventoryReportSnapshot(Guid.NewGuid(), now, 20, 5, 15, 30, 10,
            1200, 0, true, [], [], [], [], [], [], []);
        var export = InventoryReportExport.Create(snapshot, "Test tenant", "All products / Last 30 days");
        export.GeneratedAt.Should().Be(now);
        export.TenantLabel.Should().Be("Test tenant");
        export.Charts.Should().HaveCount(2);
        export.Charts[0].Values.Should().Equal(20, 5, 15);
        export.Charts[1].Values.Should().Equal(30, 10);
        export.Sections[1].Heading.Should().Contain("0 of 1200");
        export.Sections.SelectMany(x => x.Rows).Should().OnlyContain(x => x.All(cell => cell != null));
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation(string uri) => Initialize("https://portal.example/", uri);
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException();
    }
}
