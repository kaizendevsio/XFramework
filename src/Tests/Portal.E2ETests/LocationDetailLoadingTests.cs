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
using XFramework.Portal.Features.Inventario.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class LocationDetailLoadingTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task Reload_OptionalFeatures_OnlyQueriesEnabledLookups(bool reservations, bool variants)
    {
        await using var fixture = new Fixture(reservations, variants);
        var html = await fixture.Render();
        html.Should().Contain("Location Summary").And.NotContain("Could not load location");
        fixture.ProductNames.Should().ContainValue("QA Shirt");
        fixture.Data.Verify(x => x.Query<Reservation>(), reservations ? Times.Once() : Times.Never());
        fixture.Data.Verify(x => x.Query<ProductVariation>(), variants ? Times.Once() : Times.Never());
    }

    [Test]
    public async Task Reload_ReadFailure_HidesPartialDataAndRetryRecovers()
    {
        await using var fixture = new Fixture(false, false);
        fixture.Products.SetupSequence(x => x.ToListAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Private backend error"))
            .ReturnsAsync([fixture.Product]);
        var html = await fixture.Render();
        html.Should().Contain("Retry location").And.NotContain("Location Summary")
            .And.NotContain("Private backend error");
        html = await fixture.Reload();
        html.Should().Contain("Location Summary").And.NotContain("Retry location");
        fixture.ProductNames.Should().ContainValue("QA Shirt");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly HtmlRenderer _renderer;
        private readonly PageActivator _activator = new();
        private Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent _root;
        private readonly InventoryLocation _location;
        public Product Product { get; }
        public Mock<IDataContext> Data { get; } = new(MockBehavior.Strict);
        public Mock<IRemoteQuery<Product>> Products { get; }
        public Dictionary<Guid, string> ProductNames => (Dictionary<Guid, string>)typeof(LocationDetail)
            .GetField("_productNames", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_activator.Page)!;

        public Fixture(bool reservations, bool variants)
        {
            var tenantId = Guid.NewGuid();
            var warehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = tenantId, Name = "My stock", Code = "STOCK" };
            _location = new() { Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id, Name = "General stock", Code = "STOCK" };
            Product = new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "QA Shirt" };
            Query(Data, new List<Warehouse> { warehouse });
            Query(Data, new List<InventoryLocation> { _location });
            Products = Query(Data, new List<Product> { Product });
            Query(Data, new List<StockBalance> { new() { Id = Guid.NewGuid(), TenantId = tenantId, ProductId = Product.Id, LocationId = _location.Id, OnHandQuantity = 6, AvailableQuantity = 6 } });
            Query<InventoryMovement>(Data, []);
            if (reservations) Query<Reservation>(Data, []);
            if (variants) Query<ProductVariation>(Data, []);
            var modules = new Mock<IPortalModuleAvailability>();
            modules.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            modules.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>()))
                .Returns((string _, string? feature) => feature switch { "reservations" => reservations, "variations" => variants, _ => true });
            var tenant = new Mock<IPortalTenantContext>();
            tenant.SetupGet(x => x.SelectedTenantId).Returns(tenantId);
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
            _root = await _renderer.RenderComponentAsync<LocationDetail>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Id"] = _location.Id }));
            return _root.ToHtmlString();
        });

        public Task<string> Reload() => _renderer.Dispatcher.InvokeAsync(async () =>
        {
            await (Task)typeof(LocationDetail).GetMethod("Reload", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_activator.Page, null)!;
            return _root.ToHtmlString();
        });

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
        public LocationDetail? Page { get; private set; }
        public IComponent CreateInstance(Type componentType)
        {
            var component = (IComponent)Activator.CreateInstance(componentType)!;
            if (component is LocationDetail page) Page = page;
            return component;
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/inventario/locations");
    }

    private sealed class StaticJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => throw new InvalidOperationException();
    }
}
