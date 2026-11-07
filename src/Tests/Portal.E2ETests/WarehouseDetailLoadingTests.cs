using System.Linq.Expressions;
using System.Reflection;
using BlazorBlueprint.Components;
using FluentAssertions;
using Inventario.Integration.Drivers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.Portal.Features.Inventario.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class WarehouseDetailLoadingTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task Reload_OptionalFeatures_OnlyQueriesEnabledLookupsAndLoadsStock(bool variants, bool lots)
    {
        await using var fixture = new Fixture(variants, lots);
        var html = await fixture.Render();

        html.Should().Contain("General stock").And.Contain("Warehouse Summary")
            .And.NotContain("Could not load warehouse");
        fixture.Count<InventoryLocation>("_locations").Should().Be(1);
        fixture.Count<StockBalance>("_balances").Should().Be(1);
        fixture.Count<InventoryMovement>("_movements").Should().Be(1);
        fixture.Data.Verify(x => x.Query<ProductVariation>(), variants ? Times.Once() : Times.Never());
        fixture.Data.Verify(x => x.Query<InventoryLot>(), lots ? Times.Once() : Times.Never());
    }

    [Test]
    public async Task Reload_ReadFailure_HidesPartialCountsAndRetryRestoresWarehouse()
    {
        await using var fixture = new Fixture(true, false);
        fixture.Locations.SetupSequence(x => x.ToListAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Private backend error"))
            .ReturnsAsync([fixture.Location]);

        var html = await fixture.Render();
        html.Should().Contain("Could not load warehouse").And.Contain("Retry warehouse")
            .And.NotContain("Warehouse Summary").And.NotContain("Stock Balances")
            .And.NotContain("Private backend error");

        html = await fixture.Reload();
        html.Should().Contain("Warehouse Summary").And.Contain("General stock")
            .And.NotContain("Could not load warehouse");
        fixture.Count<StockBalance>("_balances").Should().Be(1);
        fixture.Count<InventoryMovement>("_movements").Should().Be(1);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly HtmlRenderer _renderer;
        private readonly PageActivator _activator = new();
        private Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent _root;
        private readonly Warehouse _warehouse = new() { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "My stock", Code = "STOCK" };
        public Mock<IDataContext> Data { get; } = new(MockBehavior.Strict);
        public InventoryLocation Location { get; }
        public Mock<IRemoteQuery<InventoryLocation>> Locations { get; }

        public Fixture(bool variants, bool lots)
        {
            Location = new() { Id = Guid.NewGuid(), TenantId = _warehouse.TenantId, WarehouseId = _warehouse.Id, Code = "STOCK", Name = "General stock" };
            var product = new Product { Id = Guid.NewGuid(), TenantId = _warehouse.TenantId, Name = "T-shirt" };
            Query(Data, new List<Warehouse> { _warehouse });
            Locations = Query(Data, new List<InventoryLocation> { Location });
            Query(Data, new List<Product> { product });
            Query(Data, new List<StockBalance> { new() { Id = Guid.NewGuid(), TenantId = _warehouse.TenantId, WarehouseId = _warehouse.Id, LocationId = Location.Id, ProductId = product.Id, OnHandQuantity = 10, AvailableQuantity = 10 } });
            Query(Data, new List<InventoryMovement> { new() { Id = Guid.NewGuid(), TenantId = _warehouse.TenantId, WarehouseId = _warehouse.Id, LocationId = Location.Id, ProductId = product.Id, MovementType = InventoryMovementType.Receipt, QuantityDelta = 10, QuantityAfter = 10 } });
            if (variants) Query<ProductVariation>(Data, []);
            if (lots) Query<InventoryLot>(Data, []);
            var modules = new Mock<IPortalModuleAvailability>();
            modules.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            modules.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>()))
                .Returns((string _, string? feature) => feature switch { "variations" => variants, "traceability" => lots, _ => true });
            var tenant = new Mock<IPortalTenantContext>();
            tenant.SetupGet(x => x.SelectedTenantId).Returns(_warehouse.TenantId);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddBlazorBlueprintComponents();
            services.AddSingleton<IJSRuntime, StaticJavaScript>();
            services.AddSingleton<NavigationManager, TestNavigation>();
            services.AddSingleton<IComponentActivator>(_activator);
            services.AddSingleton(Data.Object);
            services.AddSingleton(modules.Object);
            services.AddSingleton(tenant.Object);
            services.AddSingleton(new Mock<IInventarioServiceWrapper>(MockBehavior.Strict).Object);
            _provider = services.BuildServiceProvider();
            _renderer = new HtmlRenderer(_provider, _provider.GetRequiredService<ILoggerFactory>());
        }

        public Task<string> Render() => _renderer.Dispatcher.InvokeAsync(async () =>
        {
            _root = await _renderer.RenderComponentAsync<WarehouseDetail>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Id"] = _warehouse.Id }));
            return _root.ToHtmlString();
        });

        public Task<string> Reload() => _renderer.Dispatcher.InvokeAsync(async () =>
        {
            await (Task)typeof(WarehouseDetail).GetMethod("Reload", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_activator.Page, null)!;
            return _root.ToHtmlString();
        });

        public int Count<T>(string field) => ((List<T>)typeof(WarehouseDetail)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_activator.Page)!).Count;

        public async ValueTask DisposeAsync()
        {
            await _renderer.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    private static Mock<IRemoteQuery<T>> Query<T>(Mock<IDataContext> data, List<T> rows) where T : class
    {
        var query = new Mock<IRemoteQuery<T>>(MockBehavior.Strict);
        query.Setup(x => x.IgnoreQueryFilters()).Returns(query.Object);
        query.Setup(x => x.NoCache()).Returns(query.Object);
        query.Setup(x => x.Where(It.IsAny<Expression<Func<T, bool>>>())).Returns(query.Object);
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, string>>>())).Returns(query.Object);
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, Guid>>>())).Returns(query.Object);
        query.Setup(x => x.OrderByDescending(It.IsAny<Expression<Func<T, DateTime>>>())).Returns(query.Object);
        query.Setup(x => x.Take(It.IsAny<int>())).Returns(query.Object);
        query.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        query.Setup(x => x.FirstOrDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows.FirstOrDefault());
        data.Setup(x => x.Query<T>()).Returns(query.Object);
        return query;
    }

    private sealed class PageActivator : IComponentActivator
    {
        public WarehouseDetail? Page { get; private set; }
        public IComponent CreateInstance(Type componentType)
        {
            var component = (IComponent)Activator.CreateInstance(componentType)!;
            if (component is WarehouseDetail page) Page = page;
            return component;
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/inventario/warehouses");
    }

    private sealed class StaticJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => throw new InvalidOperationException();
    }
}
