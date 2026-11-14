using System.Net;
using BlazorBlueprint.Components;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using Moq;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Domain.Shared.Enums;
using POS.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Portal.Features.POS.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture, Category("Module:POS")]
public sealed class PosSaleDetailLoadingTests
{
    [TestCase(false)]
    [TestCase(true)]
    [Category("Area:PortalContract")]
    public async Task ContextChanged_DelayedResponse_RendersWithoutAnotherUserEvent(bool fail)
    {
        await using var fixture = new Fixture();
        await fixture.Render();
        await fixture.SelectTenant(fixture.Sale.TenantId);
        await fixture.Complete(fail);

        var html = await fixture.Html();
        html.Should().Contain(fail ? "Sale unavailable" : "Receipt Totals");
        if (!fail) html.Should().Contain(fixture.Sale.SaleNumber);
    }

    [Test, Category("Area:PortalContract")]
    public async Task ContextChanged_TenantCleared_DiscardsLateSale()
    {
        await using var fixture = new Fixture();
        await fixture.Render();
        await fixture.SelectTenant(fixture.Sale.TenantId);
        await fixture.SelectTenant(null);
        await fixture.Complete(false);

        (await fixture.Html()).Should().NotContain("Receipt Totals").And.NotContain(fixture.Sale.SaleNumber);
        fixture.POS.Verify(x => x.GetPosCheckoutSettings(It.IsAny<GetPosCheckoutSettingsRequest>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test, Category("Area:PortalContract")]
    public async Task ModuleChanged_Disabled_ClearsPreviouslyRenderedSale()
    {
        await using var fixture = new Fixture();
        await fixture.Render();
        await fixture.SelectTenant(fixture.Sale.TenantId);
        await fixture.Complete(false);
        (await fixture.Html()).Should().Contain("Receipt Totals");

        await fixture.DisableModule();

        (await fixture.Html()).Should().NotContain("Receipt Totals").And.NotContain(fixture.Sale.SaleNumber);
    }

    [Test, Category("Area:PortalLayoutBrowser")]
    public async Task PaymentAudit_AttemptedCapturedAndRefundedAmounts_MatchEachPaymentState()
    {
        await using var fixture = new Fixture();
        fixture.Sale.Payments.Clear();
        foreach (var status in new[] { PosPaymentStatus.Pending, PosPaymentStatus.Failed, PosPaymentStatus.Captured, PosPaymentStatus.Refunded })
            fixture.Sale.Payments.Add(new() { Id = Guid.NewGuid(), Status = status, Amount = 12.50m,
                RefundedAmount = status == PosPaymentStatus.Refunded ? 12.50m : 0m });
        await fixture.Render();
        await fixture.SelectTenant(fixture.Sale.TenantId);
        await fixture.Complete(false);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(await fixture.Html());
        var grid = page.GetByRole(AriaRole.Grid).Filter(new() { Has = page.GetByRole(AriaRole.Columnheader, new() { Name = "Attempted Amount" }) });
        var headers = await grid.GetByRole(AriaRole.Columnheader).AllTextContentsAsync();
        var attempted = headers.ToList().FindIndex(x => x.Contains("Attempted Amount"));
        var captured = headers.ToList().FindIndex(x => x.Contains("Captured Amount"));
        var refunded = headers.ToList().FindIndex(x => x.Contains("Refunded Amount"));
        new[] { attempted, captured, refunded }.Should().OnlyContain(index => index >= 0);
        foreach (var (status, capture, refund) in new[] {
            ("Pending", "-", "0.00"), ("Failed", "0.00", "0.00"),
            ("Captured", "12.50", "0.00"), ("Refunded", "12.50", "12.50") })
        {
            var row = grid.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Gridcell, new() { Name = status, Exact = true }) });
            var cells = row.GetByRole(AriaRole.Gridcell);
            (await cells.Nth(attempted).InnerTextAsync()).Should().Be("12.50");
            (await cells.Nth(captured).InnerTextAsync()).Should().Be(capture);
            (await cells.Nth(refunded).InnerTextAsync()).Should().Be(refund);
        }
    }

    [TestCase(PosSaleStatus.PaymentCaptured, true)]
    [TestCase(PosSaleStatus.InventoryFulfillmentFailed, true)]
    [TestCase(PosSaleStatus.Completed, false)]
    [TestCase(PosSaleStatus.PaymentPending, false)]
    [Category("Area:PortalContract")]
    public async Task RetryFulfillment_OfferedOnlyOnPaidUnfulfilledReceipt(PosSaleStatus status, bool offered)
    {
        await using var fixture = new Fixture();
        fixture.Sale = fixture.Sale with { Status = status };
        await fixture.Render();
        await fixture.SelectTenant(fixture.Sale.TenantId);
        await fixture.Complete(false);
        (await fixture.Html()).Contains("Retry Fulfillment").Should().Be(offered);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public PosSaleReceiptResponse Sale { get; set; } = PosReceiptTests.Sale();
        public Mock<IPOSServiceWrapper> POS { get; } = new(MockBehavior.Strict);
        private readonly Mock<IPortalTenantContext> _tenant = new();
        private readonly Mock<IPortalModuleAvailability> _modules = new();
        private readonly TaskCompletionSource<QueryResponse<PosSaleReceiptResponse>> _response = new();
        private readonly ServiceProvider _provider;
        private readonly HtmlRenderer _renderer;
        private HtmlRootComponent _root;
        private Guid? _selectedTenant;
        private bool _enabled = true;

        public Fixture()
        {
            _tenant.SetupGet(x => x.SelectedTenantId).Returns(() => _selectedTenant);
            _modules.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _modules.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>())).Returns(() => _enabled);
            POS.Setup(x => x.GetPosSale(It.IsAny<GetPosSaleRequest>(), It.IsAny<CancellationToken>())).Returns(_response.Task);
            POS.Setup(x => x.GetPosCheckoutSettings(It.IsAny<GetPosCheckoutSettingsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResponse<PosCheckoutSettingsResponse>
                {
                    HttpStatusCode = HttpStatusCode.OK,
                    Response = new() { TenantId = Sale.TenantId }
                });
            // Reference-label failure is nonfatal and must not keep the sale loading.
            POS.Setup(x => x.GetPosRegister(It.IsAny<GetPosRegisterRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Reference service unavailable"));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddBlazorBlueprintComponents();
            services.AddSingleton(Mock.Of<IJSRuntime>());
            services.AddSingleton<NavigationManager, TestNavigation>();
            services.AddSingleton(Mock.Of<IDataContext>());
            services.AddSingleton(POS.Object);
            services.AddSingleton(_tenant.Object);
            services.AddSingleton(_modules.Object);
            _provider = services.BuildServiceProvider();
            _renderer = new HtmlRenderer(_provider, _provider.GetRequiredService<ILoggerFactory>());
        }

        public Task Render() => _renderer.Dispatcher.InvokeAsync(async () =>
            _root = await _renderer.RenderComponentAsync<SaleDetail>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Id"] = Sale.Id })));

        public Task SelectTenant(Guid? id) => _renderer.Dispatcher.InvokeAsync(() =>
        {
            _selectedTenant = id;
            _tenant.Raise(x => x.OnChanged += null);
        });

        public Task DisableModule() => _renderer.Dispatcher.InvokeAsync(() =>
        {
            _enabled = false;
            _modules.Raise(x => x.OnChanged += null);
        });

        public Task Complete(bool fail) => _renderer.Dispatcher.InvokeAsync(() => _response.SetResult(new()
        {
            HttpStatusCode = fail ? HttpStatusCode.NotFound : HttpStatusCode.OK,
            Response = fail ? null : Sale
        }));

        public Task<string> Html() => _renderer.Dispatcher.InvokeAsync(() => _root.ToHtmlString());

        public async ValueTask DisposeAsync()
        {
            await _renderer.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/pos/sales");
    }
}
