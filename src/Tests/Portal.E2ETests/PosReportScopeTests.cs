using FluentAssertions;
using Microsoft.AspNetCore.Components;
using POS.Domain.Shared.Contracts.Responses;
using POS.Domain.Shared.Enums;
using XFramework.Portal.Features.POS.Reports;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class PosReportScopeTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ShareUri_RoundTripsAllFiltersWithoutCredentialsOrChangingTenant(bool overview)
    {
        var scope = PosReportScope.Period(Guid.NewGuid(), "month", DateTime.UtcNow) with
        { RegisterId = Guid.NewGuid(), CurrencyId = Guid.NewGuid(), Status = PosSaleStatus.PaymentFailed, Page = 3,
            PaymentMethod = PosPaymentMethod.WalletTransfer, RefundPage = 2 };
        var navigation = new TestNavigation();
        var uri = scope.ShareUri(navigation, overview);
        PosReportScope.Parse(uri, Guid.NewGuid(), overview).Should().Be(scope);
        uri.Should().NotContain("token").And.NotContain("secret");
        uri.Should().Contain(overview ? "pos/overview" : "pos/reports");
        scope.Request().Metadata.RequestedTenantId.Should().Be(scope.TenantId);
        scope.Request().Page.Should().Be(3);
        navigation.Uri.Should().Contain("secret");
    }

    [TestCase("?tenant=invalid")]
    [TestCase("?from=2026-01-01")]
    [TestCase("?tenant={tenant}&register=")]
    [TestCase("?tenant={tenant}&currency=00000000-0000-0000-0000-000000000000")]
    [TestCase("?tenant={tenant}&from=2026-02-01&to=2026-01-01")]
    [TestCase("?tenant={tenant}&from=2025-01-01&to=2026-01-02")]
    [TestCase("?tenant={tenant}&from=bad")]
    [TestCase("?tenant={tenant}&to=9999-12-31")]
    [TestCase("?tenant={tenant}&status=999")]
    [TestCase("?tenant={tenant}&page=0")]
    [TestCase("?tenant={tenant}&page=100001")]
    [TestCase("?tenant={tenant}&page=1&page=2")]
    [TestCase("?tenant={tenant}&payment=99")]
    [TestCase("?tenant={tenant}&hour=24")]
    [TestCase("?tenant={tenant}&refundPage=0")]
    public void Parse_MalformedSavedLink_FailsClosed(string query)
    {
        var parse = () => PosReportScope.Parse("https://portal.example/pos/reports" + query.Replace("{tenant}", Guid.NewGuid().ToString()), Guid.NewGuid(), false);
        parse.Should().Throw<FormatException>();
    }

    [TestCase("today", "2026-10-21")]
    [TestCase("week", "2026-10-19")]
    [TestCase("month", "2026-10-01")]
    public void Period_UsesUtcCalendarBoundariesAndMondayWeek(string period, string expected)
    {
        var scope = PosReportScope.Period(Guid.NewGuid(), period, new DateTime(2026, 10, 21, 17, 30, 0, DateTimeKind.Utc));
        scope.FromUtc.ToString("yyyy-MM-dd").Should().Be(expected);
        scope.ToUtcExclusive.Should().Be(new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc));
        scope.FromUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void HourlyShare_RoundTripsHourAndRefundPage()
    {
        var scope = PosReportScope.Period(Guid.NewGuid(), "today", DateTime.UtcNow) with
            { HourUtc = 9, RefundPage = 3, PaymentMethod = PosPaymentMethod.CashDrawer };
        var parsed = PosReportScope.Parse(scope.ShareUri(new TestNavigation()), null, false);
        parsed.Should().Be(scope);
        parsed!.Request().HourUtc.Should().Be(9);
        parsed.Request().RefundPage.Should().Be(3);
    }

    [Test]
    public void Export_KeepsCurrenciesSeparateAndClearlyBoundsReceiptPage()
    {
        var usd = Guid.NewGuid(); var sgd = Guid.NewGuid();
        var scope = PosReportScope.Period(Guid.NewGuid(), "today", DateTime.UtcNow) with { Page = 2 };
        var report = new PosSalesReportResponse(scope.TenantId, DateTime.UtcNow, scope.FromUtc, scope.ToUtcExclusive,
            200, 2, 25,
            [new(usd, 2, 20, 0, 1, 21, 21, 0) { RefundTotal = 30, RefundCount = 1 }, new(sgd, 3, 40, 2, 2, 40, 0, 40)],
            [new(usd, scope.FromUtc, 2, 21) { RefundTotal = 30 }, new(sgd, scope.FromUtc, 3, 40)], [], []);
        var model = PosReportExport.Create(report, scope, "Tenant", id => id == usd ? "USD" : "SGD");
        model.GeneratedAt.Should().Be(report.GeneratedAtUtc);
        model.Charts.Should().HaveCount(2);
        model.Charts[0].Values.Should().Equal(-9);
        model.Charts[1].Values.Should().Equal(40);
        model.Sections[0].Rows.Select(row => row[0]).Should().Equal("USD", "SGD");
        model.Sections[2].Heading.Should().Contain("page 2, 0 of 200");
        model.Scope.Should().Contain("including refunds of older sales");
        model.Sections[0].Rows[0][4].Should().Be("-9.00");
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://portal.example/", "https://portal.example/pos/reports?token=secret");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException();
    }
}
