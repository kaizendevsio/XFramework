using System.Collections;
using System.Net;
using System.Reflection;
using BlazorBlueprint.Components;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Integration.Drivers;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Portal.Features.POS.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
[Category("Module:POS")]
public sealed class PosScannerCashierIntegrationTests
{
    private Cashier page = null!;
    private ServiceProvider services = null!;
    private HookRenderer renderer = null!;
    private Mock<IPOSServiceWrapper> wrapper = null!;
    private Mock<IJSObjectReference> script = null!;
    private Guid? tenant;
    private PosRegisterResponse register = null!;
    private PosCatalogItemResponse item = null!;
    private Func<Task<QueryResponse<List<PosCatalogItemResponse>>>> reply = null!;
    private readonly List<SearchPosCatalogRequest> requests = [];

    [SetUp]
    public void Setup()
    {
        requests.Clear();
        tenant = Guid.NewGuid();
        register = new() { Id = Guid.NewGuid(), CurrencyId = Guid.NewGuid() };
        item = new() { ProductId = Guid.NewGuid(), SKU = "SKU-1", DisplayName = "Fixture item", Price = 12.50m, IsAvailable = true };
        reply = () => Task.FromResult(Success(item));
        wrapper = new(MockBehavior.Strict);
        wrapper.Setup(w => w.SearchPosCatalog(It.IsAny<SearchPosCatalogRequest>(), It.IsAny<CancellationToken>()))
            .Returns((SearchPosCatalogRequest request, CancellationToken _) => { requests.Add(request); return reply(); });
        script = new();
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddBlazorBlueprintComponents();
        services = collection.BuildServiceProvider();
        page = new EmptyCashier();
        SetProperty("POS", wrapper.Object);
        SetProperty("ToastService", services.GetRequiredService<ToastService>());
        var context = new Mock<IPortalTenantContext>();
        context.SetupGet(t => t.SelectedTenantId).Returns(() => tenant);
        SetProperty("TenantFilter", context.Object);
        SetProperty("ModuleNavigation", Mock.Of<IPortalModuleAvailability>());
        SetProperty("ActorContext", Mock.Of<IPortalActorContext>(a => a.CredentialId == Guid.NewGuid()));
        SetProperty("Logger", NullLogger<Cashier>.Instance);
        Set("_scriptTask", Task.FromResult(script.Object));
        Set("_loading", false);
        Set("_hasTenant", true);
        Set("_moduleEnabled", true);
        Set("_registers", new List<PosRegisterResponse> { register });
        Set("_selectedRegisterId", register.Id.ToString());
        Set("_currencies", new List<CurrencyType> { new() { Id = register.CurrencyId, CurrencyIsoCode3 = "USD" } });
        Set("_financeSettings", new IdentityServer.Domain.Shared.Contracts.TenantFinanceSettings
        { DefaultCurrencyId = register.CurrencyId, SupportedCurrencyIds = [register.CurrencyId] });
        Set("_checkoutSettings", new POS.Domain.Shared.Contracts.PosCheckoutSettings());
        renderer = new(services);
        renderer.Attach(page);
    }

    [TearDown]
    public async Task Stop() { await renderer.DisposeAsync(); await services.DisposeAsync(); }

    [Test]
    public async Task ReceiveMobileScan_ExactSku_UsesAuthoritativeCatalogPrice_NoDomReadOrFinancialCall()
    {
        Set("_selectedCategoryId", Guid.NewGuid());
        var generation = Field<long>("_scannerSaleGeneration");
        await Receive("sku-1");
        requests.Should().ContainSingle();
        requests[0].Search.Should().Be("sku-1");
        requests[0].CategoryId.Should().BeNull();
        requests[0].RegisterId.Should().Be(register.Id);
        requests[0].Metadata.RequestedTenantId.Should().Be(tenant);
        Cart.Count.Should().Be(1);
        Line<decimal>("UnitPrice").Should().Be(12.50m);
        Field<bool>("_searching").Should().BeFalse("the accepted-version check runs after search finally");
        Field<long>("_scannerSaleGeneration").Should().Be(generation,"normal item additions must keep the pairing");
        script.Verify(s => s.InvokeAsync<string>("readSearchValue", It.IsAny<object?[]?>()), Times.Never);
        wrapper.Verify(w => w.SearchPosCatalog(It.IsAny<SearchPosCatalogRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        wrapper.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ReceiveMobileScan_DistinctSameItemCallbacks_IncrementQuantityWithoutResettingPairing()
    {
        await Receive("SKU-1");
        await Receive("SKU-1");
        Cart.Count.Should().Be(1);
        Line<decimal>("Quantity").Should().Be(2);
        Field<long>("_scannerSaleGeneration").Should().Be(0);
    }

    [TestCase("missing")]
    [TestCase("ambiguous")]
    [TestCase("unavailable")]
    [TestCase("url")]
    [TestCase("price-payload")]
    public async Task ReceiveMobileScan_DeliberateCatalogRejection_CompletesWithoutAddingOrNavigating(string scenario)
    {
        reply = () => Task.FromResult(scenario switch
        {
            "missing" => Success(),
            "ambiguous" => Success(item, item with { ProductId = Guid.NewGuid() }),
            "unavailable" => Success(item with { IsAvailable = false }),
            _ => Success(item)
        });
        var code = scenario switch { "url" => "https://untrusted.invalid/pay", "price-payload" => "{\"SKU\":\"SKU-1\",\"price\":0.01}", _ => "SKU-1" };
        await Receive(code);
        Cart.Count.Should().Be(0);
        requests.Should().ContainSingle(r => r.Search == code);
        wrapper.Verify(w => w.SearchPosCatalog(It.IsAny<SearchPosCatalogRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        wrapper.VerifyNoOtherCalls();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReceiveMobileScan_CaughtCatalogOrTransportFailure_ThrowsInsteadOfAcknowledging(bool exception)
    {
        reply = () => exception
            ? Task.FromException<QueryResponse<List<PosCatalogItemResponse>>>(new InvalidOperationException("Fixture transport failure"))
            : Task.FromResult(new QueryResponse<List<PosCatalogItemResponse>> { HttpStatusCode = HttpStatusCode.ServiceUnavailable });
        await AssertFailure();
        Field<string>("_catalogError").Should().NotBeNullOrEmpty();
        Field<bool>("_searching").Should().BeFalse();
        Cart.Count.Should().Be(0);
    }

    [TestCase("_paymentOpen", true)]
    [TestCase("_tablePickerOpen", true)]
    [TestCase("_checkingOut", true)]
    [TestCase("_savingCart", true)]
    [TestCase("_actingOnCart", true)]
    [TestCase("_loading", true)]
    [TestCase("_searching", true)]
    [TestCase("_disposed", true)]
    [TestCase("_hasTenant", false)]
    [TestCase("_moduleEnabled", false)]
    [TestCase("_loadError", "Fixture setup failure")]
    [TestCase("_selectedRegisterId", null)]
    public async Task ReceiveMobileScan_PausedState_ThrowsBeforeLookup(string field, object? value)
    {
        Set(field,value);
        await AssertFailure();
        requests.Should().BeEmpty();
        Cart.Count.Should().Be(0);
    }

    [TestCase("tenant")]
    [TestCase("register")]
    [TestCase("new-sale")]
    [TestCase("resume-sale")]
    [TestCase("module-disabled")]
    [TestCase("loading")]
    [TestCase("busy")]
    [TestCase("disposed")]
    [TestCase("superseded")]
    [TestCase("tenant-away-and-back")]
    public async Task ReceiveMobileScan_ContextChangesWhileAwaiting_ThrowsBeforeExactSkuMutation(string change)
    {
        var pending = new TaskCompletionSource<QueryResponse<List<PosCatalogItemResponse>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        reply = () => pending.Task;
        var receive = Receive("SKU-1");
        requests.Should().ContainSingle();
        switch(change)
        {
            case "tenant": tenant = Guid.NewGuid(); break;
            case "register": Set("_selectedRegisterId",Guid.NewGuid().ToString()); break;
            case "new-sale": Invoke("ClearCurrentCart"); break;
            case "resume-sale": Invoke("ApplyCart",new PosCartResponse { RegisterId = register.Id }); break;
            case "module-disabled": Set("_moduleEnabled",false); break;
            case "loading": Set("_loading",true); break;
            case "busy": Set("_checkingOut",true); break;
            case "disposed": Set("_disposed",true); break;
            case "superseded":
                reply = () => Task.FromResult(Success(item));
                Set("_catalogSearch","manual search");
                await renderer.Dispatcher.InvokeAsync(() => (Task)Invoke("SearchCatalog",false,false,false)!);
                break;
            case "tenant-away-and-back":
                Set("_reloading",true);
                var original = tenant;
                tenant = Guid.NewGuid();
                Invoke("OnTenantChanged");
                tenant = original;
                Set("_reloading",false);
                break;
        }
        pending.SetResult(Success(item));
        await ((Func<Task>)(() => receive)).Should().ThrowAsync<InvalidOperationException>();
        Cart.Count.Should().Be(0);
    }

    [Test]
    public async Task ReceiveMobileScan_PaymentOpenedAndClosedWhileAwaiting_DoesNotAddToStageOrNewCart()
    {
        await Receive("SKU-1");
        var generation = Field<long>("_scannerSaleGeneration");
        var pending = new TaskCompletionSource<QueryResponse<List<PosCatalogItemResponse>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        reply = () => pending.Task;
        var receive = Receive("SKU-1");
        await renderer.Dispatcher.InvokeAsync(() => (Task)Invoke("BeginPayment")!);
        Field<bool>("_paymentOpen").Should().BeTrue();
        await renderer.Dispatcher.InvokeAsync(() => Invoke("PaymentOpenChanged",false));
        pending.SetResult(Success(item));
        await ((Func<Task>)(() => receive)).Should().ThrowAsync<InvalidOperationException>();
        Line<decimal>("Quantity").Should().Be(1);
        Field<bool>("_searching").Should().BeFalse();
        Field<long>("_scannerSaleGeneration").Should().Be(generation,"Back to cart does not end the sale pairing");
    }

    [Test]
    public async Task ClearCurrentCart_RotatesPairingKey_InvalidatesLookupAndClosesPayment()
    {
        await Receive("SKU-1");
        Set("_paymentOpen",true);
        var version = Field<int>("_searchVersion");
        Invoke("ClearCurrentCart");
        Field<long>("_scannerSaleGeneration").Should().Be(1);
        Field<int>("_searchVersion").Should().BeGreaterThan(version);
        Field<bool>("_paymentOpen").Should().BeFalse();
        Field<bool>("_searching").Should().BeFalse();
        Cart.Count.Should().Be(0);
    }

    [TestCase("failure")]
    [TestCase("unusable")]
    [TestCase("success")]
    [TestCase("exception")]
    public async Task Reload_TenantChangesDuringFinanceLoad_DiscardsOldResponseAndDrainsQueuedReload(string outcome)
    {
        var oldTenant = tenant!.Value;
        var newTenant = Guid.NewGuid();
        var oldReply = new TaskCompletionSource<QueryResponse<TenantFinanceSettingsResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newReply = new TaskCompletionSource<QueryResponse<TenantFinanceSettingsResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var financeRequests = new List<Guid?>();
        var identity = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        identity.Setup(x => x.GetTenantFinanceSettings(It.IsAny<GetTenantFinanceSettingsRequest>(), It.IsAny<CancellationToken>()))
            .Returns((GetTenantFinanceSettingsRequest request, CancellationToken _) =>
            {
                financeRequests.Add(request.Metadata.RequestedTenantId);
                if (request.Metadata.RequestedTenantId == oldTenant)
                    return oldReply.Task;
                newRequestStarted.SetResult();
                return newReply.Task;
            });
        SetProperty("Identity", identity.Object);
        var availability = new Mock<IPortalModuleAvailability>();
        availability.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        availability.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), "sales")).Returns(true);
        SetProperty("ModuleNavigation", availability.Object);

        var reload = renderer.Dispatcher.InvokeAsync(() => (Task)Invoke("Reload")!);
        financeRequests.Should().Equal(oldTenant);
        await renderer.Dispatcher.InvokeAsync(() => { tenant = newTenant; Invoke("OnTenantChanged"); });
        Field<bool>("_reloadRequested").Should().BeTrue();
        if (outcome == "exception")
            oldReply.SetException(new InvalidOperationException("Old tenant transport failure"));
        else
            oldReply.SetResult(new()
            {
                HttpStatusCode = outcome == "failure" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK,
                Message = "Old tenant failure",
                Response = new()
                {
                    TenantId = oldTenant, IsUsable = outcome == "success",
                    ConfigurationError = "Old tenant configuration",
                    Settings = new() { DefaultCurrencyId = register.CurrencyId, SupportedCurrencyIds = [register.CurrencyId] }
                }
            });
        try
        {
            await newRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                Field<string?>("_loadError").Should().BeNull();
                Field<TenantFinanceSettings?>("_financeSettings").Should().BeNull();
                Field<Guid?>("_loadedTenantId").Should().Be(newTenant);
            });
        }
        finally
        {
            newReply.SetResult(new()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = new() { TenantId = newTenant, IsUsable = false, ConfigurationError = "Configure the new tenant" }
            });
            await reload;
        }
        financeRequests.Should().Equal(oldTenant, newTenant);
        Field<string>("_loadError").Should().Be("Configure the new tenant");
        Field<bool>("_reloading").Should().BeFalse();
        Field<bool>("_reloadRequested").Should().BeFalse();
        wrapper.VerifyNoOtherCalls();
    }

    [Test]
    public async Task HoldSale_TableEnabled_OpensPickerBeforeCreatingCart()
    {
        await Receive("SKU-1");
        Set("_cartsEnabled", true);
        Set("_checkoutSettings", new POS.Domain.Shared.Contracts.PosCheckoutSettings { TableTrackingEnabled = true });
        await renderer.Dispatcher.InvokeAsync(() => (Task)Invoke("SuspendCart")!);
        Field<bool>("_tablePickerOpen").Should().BeTrue();
        Field<bool>("_holdAfterTable").Should().BeTrue();
        Cart.Count.Should().Be(1);
        wrapper.Verify(w => w.CreatePosCart(It.IsAny<CreatePosCartRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task TableSelection_RetainsTableIdentityForCheckout_WithoutFinancialCalls()
    {
        var layout = new POS.Domain.Shared.Contracts.PosTableLayout { Tables = [new() { Label = "A1" }] };
        Set("_checkoutSettings", new POS.Domain.Shared.Contracts.PosCheckoutSettings
        { TableTrackingEnabled = true, ActiveTableLayoutId = layout.Id, TableLayouts = [layout] });
        await renderer.Dispatcher.InvokeAsync(() => (Task)Invoke("TableSelected", layout.Tables[0])!);
        var metadata = (POS.Domain.Shared.Contracts.PosCheckoutMetadata)Invoke("BuildCheckoutMetadata")!;
        metadata.TableId.Should().Be(layout.Tables[0].Id);
        metadata.TableLayoutId.Should().Be(layout.Id);
        metadata.TableNumber.Should().Be("A1");
        wrapper.VerifyNoOtherCalls();
    }

    private Task Receive(string code) => renderer.Dispatcher.InvokeAsync(() => (Task)Invoke("ReceiveMobileScan",code)!);
    private async Task AssertFailure() => await ((Func<Task>)(() => Receive("SKU-1"))).Should().ThrowAsync<InvalidOperationException>();
    private static QueryResponse<List<PosCatalogItemResponse>> Success(params PosCatalogItemResponse[] items) => new()
    { HttpStatusCode = HttpStatusCode.OK, Response = items.ToList() };
    private IList Cart => Field<IList>("_cart");
    private T Line<T>(string name) => (T)Cart[0]!.GetType().GetProperty(name)!.GetValue(Cart[0])!;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private object? Invoke(string name, params object?[] args) => typeof(Cashier).GetMethod(name,Private)!.Invoke(page,args);
    private void SetProperty(string name, object value) => typeof(Cashier).GetProperty(name,Private)!.SetValue(page,value);
    private void Set(string name, object? value) => typeof(Cashier).GetField(name,Private)!.SetValue(page,value);
    private T Field<T>(string name) => (T)typeof(Cashier).GetField(name,Private)!.GetValue(page)!;
    private sealed class EmptyCashier : Cashier
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder b) { }
        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    }
    private sealed class HookRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public void Attach(IComponent component) => AssignRootComponentId(component);
        protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
        protected override void HandleException(Exception exception) => throw exception;
    }
}
