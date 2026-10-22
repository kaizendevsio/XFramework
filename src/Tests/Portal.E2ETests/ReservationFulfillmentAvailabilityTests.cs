using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using BlazorBlueprint.Components;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Inventario.Integration.Drivers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.Portal.Features.Inventario.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract"), Category("Module:Inventario")]
public sealed class ReservationFulfillmentAvailabilityTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task Render_FulfillmentAvailability_OnlyDisablesFulfill(bool detail, bool enabled)
    {
        await using var page = new PageFixture(detail, enabled);
        var html = await page.Render();

        ButtonDisabled(html, "Fulfill").Should().Be(!enabled);
        ButtonDisabled(html, "Release").Should().BeFalse();
        ButtonDisabled(html, "Cancel").Should().BeFalse();
        if (!detail) ButtonDisabled(html, "Expire Due").Should().BeFalse();
        if (!enabled) html.Should().Contain("Fulfillment is disabled for this tenant.");
        else html.Should().NotContain("Fulfillment is disabled for this tenant.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Fulfill_AvailabilityDisabledAfterRender_DoesNotDispatch(bool detail)
    {
        await using var page = new PageFixture(detail, true);
        await page.Render();
        page.FulfillmentEnabled = false;

        await page.Invoke("FulfillReservation");

        page.Wrapper.Verify(x => x.FulfillReservation(It.IsAny<FulfillReservationRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Fulfill_Enabled_DispatchesForSelectedTenantAndReservation(bool detail)
    {
        await using var page = new PageFixture(detail, true);
        page.Wrapper.Setup(x => x.FulfillReservation(It.IsAny<FulfillReservationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        await page.Render();

        await page.Invoke("FulfillReservation");

        page.Wrapper.Verify(x => x.FulfillReservation(It.Is<FulfillReservationRequest>(r =>
            r.ReservationId == page.Reservation.Id && r.Metadata.RequestedTenantId == page.Reservation.TenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Cleanup_FulfillmentDisabled_ReleaseCancelAndExpiryRemainUsable(bool detail)
    {
        await using var page = new PageFixture(detail, false);
        page.Wrapper.Setup(x => x.ReleaseReservation(It.IsAny<ReleaseReservationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        page.Wrapper.Setup(x => x.CancelReservation(It.IsAny<CancelReservationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        page.Wrapper.Setup(x => x.ExpireReservations(It.IsAny<ExpireReservationsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        await page.Render();

        await page.Invoke("ReleaseReservation");
        await page.Invoke("CancelReservation");
        if (!detail) await page.Invoke("ExpireDueReservations");

        page.Wrapper.Verify(x => x.ReleaseReservation(It.Is<ReleaseReservationRequest>(r =>
            r.ReservationId == page.Reservation.Id && r.Metadata.RequestedTenantId == page.Reservation.TenantId),
            It.IsAny<CancellationToken>()), Times.Once);
        page.Wrapper.Verify(x => x.CancelReservation(It.Is<CancelReservationRequest>(r =>
            r.ReservationId == page.Reservation.Id && r.Metadata.RequestedTenantId == page.Reservation.TenantId),
            It.IsAny<CancellationToken>()), Times.Once);
        if (!detail)
            page.Wrapper.Verify(x => x.ExpireReservations(It.Is<ExpireReservationsRequest>(r =>
                r.Metadata.RequestedTenantId == page.Reservation.TenantId), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static bool ButtonDisabled(string html, string label)
    {
        var button = Regex.Matches(html, @"<button\b[^>]*>.*?</button>", RegexOptions.Singleline)
            .Single(x => Regex.Replace(x.Value, "<[^>]+>", "").Trim() == label).Value;
        return Regex.IsMatch(button, @"\sdisabled(?:=|\s|>)");
    }

    private sealed class PageFixture : IAsyncDisposable
    {
        private readonly bool _detail;
        private readonly ServiceProvider _provider;
        private readonly HtmlRenderer _renderer;
        private readonly PageActivator _activator = new();
        public bool FulfillmentEnabled { get; set; }
        public Reservation Reservation { get; } = new()
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), ProductId = Guid.NewGuid(),
            Status = ReservationStatus.Active, Quantity = 1, ReservedAt = DateTime.UtcNow
        };
        public Mock<IInventarioServiceWrapper> Wrapper { get; } = new(MockBehavior.Strict);

        public PageFixture(bool detail, bool enabled)
        {
            _detail = detail;
            FulfillmentEnabled = enabled;
            var data = new Mock<IDataContext>(MockBehavior.Strict);
            AllowQuery(data, new List<Reservation> { Reservation });
            AllowQuery(data, new List<Product> { new() { Id = Reservation.ProductId, TenantId = Reservation.TenantId, Name = "Reserved product" } });
            AllowQuery<ProductVariation>(data, []);
            AllowQuery<StockBalance>(data, []);
            AllowQuery<Warehouse>(data, []);
            AllowQuery<InventoryLocation>(data, []);
            AllowQuery<ReservationAllocation>(data, []);
            var modules = new Mock<IPortalModuleAvailability>();
            modules.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            modules.Setup(x => x.IsFeatureEnabled(TenantModuleFeatureKeys.Inventario, "reservations")).Returns(true);
            modules.Setup(x => x.IsFeatureEnabled(TenantModuleFeatureKeys.Inventario, "fulfillment")).Returns(() => FulfillmentEnabled);
            var tenant = new Mock<IPortalTenantContext>();
            tenant.SetupGet(x => x.SelectedTenantId).Returns(Reservation.TenantId);
            Wrapper.Setup(x => x.GetReservationAllocations(It.IsAny<GetReservationAllocationsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResponse<List<ReservationAllocation>> { HttpStatusCode = HttpStatusCode.OK, Response = [] });
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddBlazorBlueprintComponents();
            services.AddSingleton<IJSRuntime, StaticJavaScript>();
            services.AddSingleton<NavigationManager, TestNavigation>();
            services.AddSingleton<IComponentActivator>(_activator);
            services.AddSingleton(data.Object);
            services.AddSingleton(modules.Object);
            services.AddSingleton(tenant.Object);
            services.AddSingleton(Wrapper.Object);
            _provider = services.BuildServiceProvider();
            _renderer = new HtmlRenderer(_provider, _provider.GetRequiredService<ILoggerFactory>());
        }

        public Task<string> Render() => _renderer.Dispatcher.InvokeAsync(async () =>
            (await _renderer.RenderComponentAsync(_detail ? typeof(ReservationDetail) : typeof(Reservations),
                _detail ? ParameterView.FromDictionary(new Dictionary<string, object?> { ["Id"] = Reservation.Id }) : ParameterView.Empty)).ToHtmlString());

        public Task Invoke(string action) => _renderer.Dispatcher.InvokeAsync(() =>
            (Task)_activator.Page!.GetType().GetMethod(action, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_activator.Page, _detail || action == "ExpireDueReservations" ? null : [Reservation.Id])!);

        public async ValueTask DisposeAsync()
        {
            await _renderer.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    private static void AllowQuery<T>(Mock<IDataContext> data, List<T> rows) where T : class
    {
        var query = new Mock<IRemoteQuery<T>>(MockBehavior.Strict);
        query.Setup(x => x.IgnoreQueryFilters()).Returns(query.Object);
        query.Setup(x => x.NoCache()).Returns(query.Object);
        query.Setup(x => x.Where(It.IsAny<Expression<Func<T, bool>>>())).Returns(query.Object);
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, string>>>())).Returns(query.Object);
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, DateTime>>>())).Returns(query.Object);
        query.Setup(x => x.OrderByDescending(It.IsAny<Expression<Func<T, DateTime>>>())).Returns(query.Object);
        query.Setup(x => x.Take(It.IsAny<int>())).Returns(query.Object);
        query.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        query.Setup(x => x.FirstOrDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows.FirstOrDefault());
        data.Setup(x => x.Query<T>()).Returns(query.Object);
    }

    private sealed class PageActivator : IComponentActivator
    {
        public IComponent? Page { get; private set; }
        public IComponent CreateInstance(Type componentType)
        {
            var component = (IComponent)Activator.CreateInstance(componentType)!;
            if (component is Reservations or ReservationDetail) Page = component;
            return component;
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/inventario/reservations");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class StaticJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => throw new InvalidOperationException();
    }
}
