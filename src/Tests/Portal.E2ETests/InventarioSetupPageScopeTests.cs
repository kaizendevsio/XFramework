using System.Net;
using System.Reflection;
using FluentAssertions;
using Inventario.Integration.Drivers;
using Microsoft.AspNetCore.Components;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Portal.Features.Inventario.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Kind:Unit"), Category("Module:Inventario"), Category("Area:Setup")]
public sealed class InventarioSetupPageScopeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Guid _tenant;
    private Mock<IPortalTenantContext> _context = null!;
    private Mock<IPortalModuleAvailability> _availability = null!;
    private Mock<IInventarioServiceWrapper> _wrapper = null!;
    private SetupNavigation _navigation = null!;

    [SetUp]
    public void Start()
    {
        _tenant = Guid.NewGuid();
        _context = new();
        _context.SetupGet(x => x.SelectedTenantId).Returns(() => _tenant);
        _availability = new();
        _availability.SetupGet(x => x.ActiveTenantId).Returns(() => _tenant);
        _availability.Setup(x => x.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _availability.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        _wrapper = new(MockBehavior.Strict);
        _navigation = new();
    }

    [TestCase(false, false, "/inventario/setup")]
    [TestCase(true, false, "/inventario/products")]
    [TestCase(false, true, "/inventario/products")]
    public async Task Entry_FreshOrExistingTenant_RoutesWithoutWrites(bool existing, bool complete, string destination)
    {
        _wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(new() { TenantId = _tenant, HasExistingConfiguration = existing, CompletedAt = complete ? DateTime.UtcNow : null }));
        await Invoke(Page<SetupEntry>(), "Load");
        _navigation.LastUri.Should().Be(destination);
        _wrapper.Verify(x => x.GetInventarioSetup(It.Is<GetInventarioSetupRequest>(r => r.Metadata.RequestedTenantId == _tenant), It.IsAny<CancellationToken>()), Times.Once);
        _wrapper.VerifyNoOtherCalls();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Entry_NoTenantOrDisabledModule_DoesNotCallWrapper(bool noTenant)
    {
        if (noTenant) _context.SetupGet(x => x.SelectedTenantId).Returns((Guid?)null);
        else _availability.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>())).Returns(false);
        await Invoke(Page<SetupEntry>(), "Load");
        _navigation.LastUri.Should().BeNull();
        _wrapper.VerifyNoOtherCalls();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Entry_TenantChangedOrDisposedDuringRead_DoesNotNavigateFromOldResponse(bool dispose)
    {
        var firstTenant = _tenant;
        var gate = new TaskCompletionSource<QueryResponse<InventarioSetupResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _wrapper.Setup(x => x.GetInventarioSetup(It.IsAny<GetInventarioSetupRequest>(), It.IsAny<CancellationToken>())).Returns(gate.Task);
        var page = Page<SetupEntry>();
        var pending = Invoke(page, "Load");
        if (dispose) page.Dispose(); else _tenant = Guid.NewGuid();
        gate.SetResult(Response(new() { TenantId = firstTenant }));
        await pending;
        _navigation.LastUri.Should().BeNull();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Settings_PendingSaveSnapshotsDraftAndTenant_AndIgnoresStaleCompletion(bool dispose)
    {
        var firstTenant = _tenant;
        var stamp = Guid.NewGuid();
        var state = new InventarioSetupResponse { TenantId = firstTenant, CanManage = true, ConcurrencyStamp = stamp };
        var gate = new TaskCompletionSource<QueryResponse<InventarioSetupResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        UpdateInventarioPreferencesRequest? captured = null;
        _wrapper.Setup(x => x.UpdateInventarioPreferences(It.IsAny<UpdateInventarioPreferencesRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateInventarioPreferencesRequest, CancellationToken>((r, _) => captured = r).Returns(gate.Task);
        var page = Page<Settings>();
        SetField(page, "_state", state); SetField(page, "_threshold", 19); SetField(page, "_currency", "SGD");
        var pending = Invoke(page, "Save");
        captured.Should().NotBeNull();
        if (dispose) page.Dispose(); else _tenant = Guid.NewGuid();
        SetField(page, "_threshold", 42); SetField(page, "_currency", "USD");
        gate.SetResult(Response(new() { TenantId = firstTenant, LowStockThreshold = 19, DefaultCurrency = "SGD" }));
        await pending;
        captured!.Metadata.RequestedTenantId.Should().Be(firstTenant);
        captured.ExpectedConcurrencyStamp.Should().Be(stamp);
        captured.LowStockThreshold.Should().Be(19); captured.DefaultCurrency.Should().Be("SGD");
        Field<int>(page, "_threshold").Should().Be(42);
        Field<string>(page, "_currency").Should().Be("USD");
        Field<InventarioSetupResponse>(page, "_state").Should().BeSameAs(state);
        _wrapper.Verify(x => x.UpdateInventarioPreferences(It.IsAny<UpdateInventarioPreferencesRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private T Page<T>() where T : new()
    {
        var page = new T();
        typeof(T).GetProperty("Inventario", Private)!.SetValue(page, _wrapper.Object);
        typeof(T).GetProperty("TenantContext", Private)!.SetValue(page, _context.Object);
        typeof(T).GetProperty("Availability", Private)!.SetValue(page, _availability.Object);
        typeof(T).GetProperty("Navigation", Private)!.SetValue(page, _navigation);
        return page;
    }
    private static Task Invoke<T>(T page, string method) => (Task)typeof(T).GetMethod(method, Private)!.Invoke(page, null)!;
    private static void SetField<T>(T page, string name, object value) => typeof(T).GetField(name, Private)!.SetValue(page, value);
    private static TValue Field<TValue>(object page, string name) => (TValue)page.GetType().GetField(name, Private)!.GetValue(page)!;
    private static QueryResponse<InventarioSetupResponse> Response(InventarioSetupResponse state) => new() { HttpStatusCode = HttpStatusCode.OK, Response = state };
    private sealed class SetupNavigation : NavigationManager
    {
        public string? LastUri { get; private set; }
        public SetupNavigation() => Initialize("http://localhost/", "http://localhost/inventario");
        protected override void NavigateToCore(string uri, NavigationOptions options) => LastUri = uri;
    }
}
