using System.Net;
using BlazorBlueprint.Components;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Portal.Features.POS.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture, Category("Area:PortalContract"), Category("Module:POS")]
public sealed class PosSaleDetailLoadingTests
{
    [TestCase(false)]
    [TestCase(true)]
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

    [Test]
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

    [Test]
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

    private sealed class Fixture : IAsyncDisposable
    {
        public PosSaleReceiptResponse Sale { get; } = PosReceiptTests.Sale();
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
