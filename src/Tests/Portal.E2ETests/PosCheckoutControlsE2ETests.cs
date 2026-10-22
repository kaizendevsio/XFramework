using System.Net;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Moq;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Domain.Shared.Enums;
using POS.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;

namespace Portal.E2ETests;

public sealed partial class PosScannerPairingDialogE2ETests
{
    private void ConfigureCheckoutIdentity(IServiceCollection services)
    {
        var modules = new Mock<IPortalModuleAvailability>();
        modules.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        modules.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        services.AddSingleton(modules.Object);
        var identity = new Mock<IIdentityServerServiceWrapper>();
        identity.Setup(x => x.GetTenantPosSettings(It.IsAny<GetTenantPosSettingsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => SettingsResponse());
        identity.Setup(x => x.UpdateTenantPosSettings(It.IsAny<UpdateTenantPosSettingsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateTenantPosSettingsRequest request, CancellationToken _) =>
            {
                request.Metadata.RequestedTenantId.Should().Be(state.TenantId);
                request.Settings.IsValid().Should().BeTrue();
                state.CheckoutSettings = request.Settings.DeepClone();
                Interlocked.Increment(ref state.SettingsSaves);
                return SettingsResponse();
            });
        identity.Setup(x => x.CreateIdentity(It.IsAny<CreateIdentityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateIdentityRequest request, CancellationToken _) =>
            {
                request.Metadata.RequestedTenantId.Should().Be(state.TenantId);
                Interlocked.Increment(ref state.IdentityCreates);
                return state.DenyCustomerCreate
                    ? new CmdResponse<IdentityAdministrationResponse> { HttpStatusCode = HttpStatusCode.Forbidden, Message = "Customer creation permission denied." }
                    : new CmdResponse<IdentityAdministrationResponse> { HttpStatusCode = HttpStatusCode.OK,
                        Response = new() { Id = state.CustomerIdentityId, TenantId = state.TenantId } };
            });
        identity.Setup(x => x.CreateCredential(It.IsAny<CreateCredentialRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateCredentialRequest request, CancellationToken _) =>
            {
                request.IdentityInfoId.Should().Be(state.CustomerIdentityId);
                request.Metadata.RequestedTenantId.Should().Be(state.TenantId);
                Interlocked.Increment(ref state.CredentialCreates);
                return new CmdResponse<CredentialAdministrationResponse> { HttpStatusCode = HttpStatusCode.OK,
                    Response = new() { Id = state.CustomerCredentialId, TenantId = state.TenantId,
                        IdentityInfoId = state.CustomerIdentityId, UserName = "table.customer", UserAlias = "Table customer", IsEnabled = true } };
            });
        services.AddSingleton(identity.Object);
    }

    private QueryResponse<TenantPosSettingsResponse> SettingsResponse() => new()
    {
        HttpStatusCode = HttpStatusCode.OK,
        Response = new() { TenantId = state.TenantId, Settings = state.CheckoutSettings, CanManage = true, IsUsable = true }
    };

    private void ConfigureHeldCartWrapper(Mock<IPOSServiceWrapper> wrapper)
    {
        wrapper.Setup(x => x.CreatePosCart(It.IsAny<CreatePosCartRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreatePosCartRequest request, CancellationToken _) =>
            {
                request.Metadata.RequestedTenantId.Should().Be(state.TenantId);
                request.Suspend.Should().BeTrue();
                state.CheckoutSettings.Validate(request.CheckoutMetadata, false, requireTable: true).Should().BeNull();
                state.HeldCart = new()
                {
                    Id = Guid.NewGuid(), TenantId = state.TenantId, RegisterId = state.RegisterId,
                    CartNumber = "HELD-12", CurrencyId = state.CurrencyId, Status = PosCartStatus.Suspended,
                    ConcurrencyStamp = Guid.NewGuid(), TotalAmount = 10, CheckoutMetadata = request.CheckoutMetadata,
                    Lines = request.Lines.Select(line => new PosCartLineResponse { ProductId = line.ProductId,
                        ProductName = state.Item.ProductName, Quantity = line.Quantity, ExpectedUnitPrice = 10 }).ToList()
                };
                return new CmdResponse<PosCartResponse> { HttpStatusCode = HttpStatusCode.OK, Response = state.HeldCart };
            });
        wrapper.Setup(x => x.SearchPosCarts(It.IsAny<SearchPosCartsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new QueryResponse<List<PosCartSummaryResponse>>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = state.HeldCart is { Status: PosCartStatus.Suspended } cart
                    ? [new() { Id = cart.Id, TenantId = cart.TenantId, CartNumber = cart.CartNumber,
                        TableNumber = cart.CheckoutMetadata.TableNumber, TotalAmount = cart.TotalAmount, ConcurrencyStamp = cart.ConcurrencyStamp }]
                    : []
            });
        wrapper.Setup(x => x.ResumePosCart(It.IsAny<ResumePosCartRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResumePosCartRequest request, CancellationToken _) =>
            {
                request.CartId.Should().Be(state.HeldCart!.Id);
                state.HeldCart = state.HeldCart with { Status = PosCartStatus.Open };
                return new CmdResponse<PosCartResponse> { HttpStatusCode = HttpStatusCode.OK, Response = state.HeldCart };
            });
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task CheckoutControls_SettingsSave_TableHeldAndResumed_RequiredFieldsAndCustomerCreate(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        // Blueprint wires text inputs asynchronously after render; wait for real interactivity, not just visibility.
        await Page.AddInitScriptAsync("""
            const originalAddListener = EventTarget.prototype.addEventListener;
            EventTarget.prototype.addEventListener = function(type, listener, options) {
                const result = originalAddListener.call(this, type, listener, options);
                if (type === 'change' && this instanceof HTMLInputElement && this.getAttribute('aria-label')?.startsWith('Field '))
                    this.dataset.posChangeReady = 'true';
                return result;
            };
            """);
        await Page.GotoAsync(app.Urls.Single() + "/pos-settings-fixture");
        var trackTables = Page.GetByRole(AriaRole.Switch, new() { Name = "Enable table selection", Exact = true });
        await trackTables.ClickAsync();
        await Expect(trackTables).ToBeCheckedAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "New layout", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add table", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Table 1, 4 seats", Exact = true })).ToBeVisibleAsync();
        await CheckoutScreenshot("table-layout-designer", width);
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Checkout fields", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add field", Exact = true }).ClickAsync();
        await Expect(Page.GetByLabel("Field key", new() { Exact = true })).ToHaveAttributeAsync("data-pos-change-ready", "true");
        await Expect(Page.GetByLabel("Field label", new() { Exact = true })).ToHaveAttributeAsync("data-pos-change-ready", "true");
        await Page.GetByLabel("Field key", new() { Exact = true }).FillAsync("coupon");
        await Page.GetByLabel("Field label", new() { Exact = true }).FillAsync("Coupon");
        await Page.GetByLabel("Field label", new() { Exact = true }).BlurAsync();
        var requireCoupon = Page.GetByRole(AriaRole.Switch, new() { Name = "Require Coupon", Exact = true });
        await requireCoupon.ClickAsync();
        await Expect(requireCoupon).ToBeCheckedAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Wait(() => Volatile.Read(ref state.SettingsSaves) == 1);
        state.CheckoutSettings.TableTrackingEnabled.Should().BeTrue();
        var savedField = state.CheckoutSettings.CheckoutFields.Should().ContainSingle().Subject;
        savedField.Key.Should().Be("coupon");
        savedField.Label.Should().Be("Coupon");
        savedField.Enabled.Should().BeTrue();
        savedField.Required.Should().BeTrue("Save must capture the checked Required switch");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Checkout fields", Exact = true }).ClickAsync();
        await Expect(Page.GetByLabel("Field key", new() { Exact = true })).ToHaveValueAsync("coupon");
        await Expect(Page.GetByLabel("Field label", new() { Exact = true })).ToHaveValueAsync("Coupon");
        await Expect(requireCoupon).ToBeCheckedAsync();
        var settingsBounds = await Page.Locator(".pos-settings-page").BoundingBoxAsync();
        settingsBounds!.Width.Should().BeLessThanOrEqualTo(1152);
        Math.Abs(settingsBounds.X - (width - settingsBounds.Width) / 2).Should().BeLessThanOrEqualTo(1);
        foreach (var label in new[] { "Field key", "Field label" })
        {
            var inputBounds = await Page.GetByLabel(label, new() { Exact = true }).BoundingBoxAsync();
            inputBounds!.Width.Should().BeGreaterThanOrEqualTo(160, "editable settings fields must not collapse at tablet width");
        }
        await CheckoutScreenshot("settings", width);

        await Page.GotoAsync(app.Urls.Single() + "/cashier-scanner-fixture");
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Hold sale", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog, new() { Name = "Select table", Exact = true })).ToBeVisibleAsync();
        state.HeldCart.Should().BeNull("holding must ask for a table before persisting the order");
        await CheckoutScreenshot("hold-table-picker", width);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Table 1, 4 seats", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Table 1", new() { Exact = true })).ToBeVisibleAsync();
        state.HeldCart!.CheckoutMetadata.TableNumber.Should().Be("1");
        state.HeldCart.CheckoutMetadata.TableId.Should().Be(state.CheckoutSettings.ActiveTableLayout!.Tables[0].Id);
        state.HeldCart.CheckoutMetadata.Fields.Should().BeEmpty("required checkout fields do not block holding a table bill");
        await CheckoutScreenshot("held-table", width);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Resume sale", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        await Page.GetByTestId("pos-checkout").ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Table 1", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Exact amount", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("pos-confirm-payment")).ToBeDisabledAsync();
        await Page.GetByLabel("Coupon", new() { Exact = false }).FillAsync("SAVE50");
        await Page.GetByLabel("Coupon", new() { Exact = false }).BlurAsync();
        await Expect(Page.GetByTestId("pos-confirm-payment")).ToBeEnabledAsync();
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        await CheckoutScreenshot("payment-fields", width);

        await Page.GetByText("Customer wallet", new() { Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Customer", Exact = true }).ClickAsync();
        await Page.GetByText("Create customer", new() { Exact = true }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Create customer", Exact = true });
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByLabel("Customer name", new() { Exact = true }).FillAsync("Table customer");
        await dialog.GetByLabel("Username", new() { Exact = true }).FillAsync("table.customer");
        await dialog.GetByLabel("Password", new() { Exact = true }).FillAsync("fixture-password-123");
        await Expect(dialog.GetByLabel("Password", new() { Exact = true })).ToHaveAttributeAsync("type", "password");
        state.DenyCustomerCreate = true;
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create customer", Exact = true }).ClickAsync();
        await Expect(dialog.GetByText("Customer creation permission denied.", new() { Exact = true })).ToBeVisibleAsync();
        state.CredentialCreates.Should().Be(0);
        await CheckoutScreenshot("customer-permission", width);
        state.DenyCustomerCreate = false;
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create customer", Exact = true }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Customer", Exact = true })).ToContainTextAsync("table.customer");
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Table 1", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Coupon", new() { Exact = false })).ToHaveValueAsync("SAVE50");
        state.IdentityCreates.Should().Be(2);
        state.CredentialCreates.Should().Be(1);
        state.FinancialCalls.Should().Be(0);
        await CheckoutScreenshot("customer-selected", width);
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task CheckoutControls_LegacyHeldTable_CanClearOptionalTableWithoutLayout(int width, int height)
    {
        state.CheckoutSettings = new() { TableTrackingEnabled = true, TableRequired = false };
        state.HeldCart = new()
        {
            Id = Guid.NewGuid(), TenantId = state.TenantId, RegisterId = state.RegisterId,
            CartNumber = "HELD-LEGACY", CurrencyId = state.CurrencyId, Status = PosCartStatus.Suspended,
            ConcurrencyStamp = Guid.NewGuid(), TotalAmount = 10,
            CheckoutMetadata = new() { TableNumber = "T12" },
            Lines = [new() { ProductId = state.Item.ProductId, ProductName = state.Item.ProductName,
                Quantity = 1, ExpectedUnitPrice = 10 }]
        };
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(app.Urls.Single() + "/cashier-scanner-fixture");
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        await state.Cashier!.ClearForFixture();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Held sales", Exact = true }).ClickAsync();
        var heldSales = Page.GetByRole(AriaRole.Dialog, new() { Name = "Held sales", Exact = true });
        await heldSales.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
        await heldSales.GetByRole(AriaRole.Button, new() { Name = "Resume sale", Exact = true }).ClickAsync();
        await Expect(heldSales).ToBeHiddenAsync();
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        await Page.GetByTestId("pos-checkout").ClickAsync();
        var payment = Page.GetByRole(AriaRole.Dialog, new() { Name = "Payment", Exact = true });
        await Expect(payment.GetByRole(AriaRole.Button, new() { Name = "Table T12", Exact = true })).ToBeVisibleAsync();
        await payment.GetByRole(AriaRole.Button, new() { Name = "Exact amount", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("pos-confirm-payment")).ToBeDisabledAsync();
        await Expect(payment.GetByText("Configure an active table layout in POS settings", new() { Exact = true })).ToBeVisibleAsync();

        await payment.GetByRole(AriaRole.Button, new() { Name = "No table", Exact = true }).ClickAsync();
        await Expect(payment.GetByRole(AriaRole.Button, new() { Name = "Select table", Exact = true })).ToBeVisibleAsync();
        await Expect(payment.GetByRole(AriaRole.Button, new() { Name = "No table", Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("pos-confirm-payment")).ToBeEnabledAsync();
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        state.FinancialCalls.Should().Be(0);
        await CheckoutScreenshot("legacy-held-table-cleared", width);
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task CheckoutControls_DisabledTableAndFields_AreAbsentAndDoNotBlockPayment(int width, int height)
    {
        state.CheckoutSettings = new() { CheckoutFields = [new() { Key = "coupon", Label = "Coupon", Enabled = false, Required = true }] };
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(app.Urls.Single() + "/cashier-scanner-fixture");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sale details", Exact = true }).ClickAsync();
        await Expect(Page.GetByLabel("Table number", new() { Exact = true })).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true }).ClickAsync();
        await Page.GetByTestId("pos-checkout").ClickAsync();
        await Expect(Page.GetByLabel("Table number", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByLabel("Coupon", new() { Exact = false })).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Exact amount", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("pos-confirm-payment")).ToBeEnabledAsync();
        await CheckoutScreenshot("disabled-fields", width);
        state.FinancialCalls.Should().Be(0);
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task CheckoutControls_TwentyRequiredFields_StackVerticallyAndScrollWithinPaymentDialog(int width, int height)
    {
        state.CheckoutSettings = new()
        {
            TableTrackingEnabled = true,
            CheckoutFields = Enumerable.Range(1, 20).Select(index => new PosCheckoutField
            {
                Key = $"field_{index}", Label = $"Checkout field {index:D2}", Enabled = true, Required = true
            }).ToList()
        };
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(app.Urls.Single() + "/cashier-scanner-fixture");
        await Page.GetByTestId("pos-checkout").ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Payment", Exact = true });
        await Expect(dialog).ToBeVisibleAsync();
        ILocator Field(int index) => dialog.GetByLabel($"Checkout field {index:D2}", new() { Exact = false });
        await Expect(dialog.GetByLabel(new System.Text.RegularExpressions.Regex("^Checkout field [0-9]{2}"))).ToHaveCountAsync(20);

        // The opening scale transition changes coordinates between sequential field measurements.
        await dialog.EvaluateAsync("""
            async element => {
                await new Promise(requestAnimationFrame);
                await Promise.all(element.getAnimations({ subtree: true })
                    .filter(animation => animation.effect.getComputedTiming().iterations !== Infinity)
                    .map(animation => animation.finished.catch(() => {})));
            }
            """);

        var previous = await dialog.GetByRole(AriaRole.Button, new() { Name = "Select table", Exact = true }).BoundingBoxAsync();
        previous.Should().NotBeNull();
        for (var index = 1; index <= 20; index++)
        {
            var bounds = await Field(index).BoundingBoxAsync();
            bounds.Should().NotBeNull();
            bounds!.Y.Should().BeGreaterThanOrEqualTo(previous!.Y + previous.Height,
                "metadata inputs must be vertically stacked without overlapping");
            if (index > 1)
            {
                Math.Abs(bounds.X - previous.X).Should().BeLessThanOrEqualTo(1);
                Math.Abs(bounds.Width - previous.Width).Should().BeLessThanOrEqualTo(1);
            }
            previous = bounds;
        }
        (await dialog.EvaluateAsync<bool>("e => e.scrollHeight > e.clientHeight && e.scrollWidth <= e.clientWidth + 1 && ['auto', 'scroll'].includes(getComputedStyle(e).overflowY)")).Should().BeTrue();
        await CheckoutScreenshot("twenty-fields-top", width);

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Exact amount", Exact = true }).ClickAsync();
        var confirm = Page.GetByTestId("pos-confirm-payment");
        await Expect(confirm).ToBeDisabledAsync();
        for (var index = 1; index < 20; index++)
            await Field(index).FillAsync($"Value {index}");
        await Field(19).BlurAsync();
        await Field(20).ScrollIntoViewIfNeededAsync();
        await Expect(Field(20)).ToBeInViewportAsync(new() { Ratio = 1 });
        await Expect(confirm).ToBeDisabledAsync();
        (await dialog.EvaluateAsync<double>("e => e.scrollTop")).Should().BeGreaterThan(0);
        await Field(20).FillAsync("Last required value");
        await Field(20).BlurAsync();
        await Expect(confirm).ToBeEnabledAsync();
        await confirm.ScrollIntoViewIfNeededAsync();
        await Expect(confirm).ToBeInViewportAsync(new() { Ratio = 1 });
        (await dialog.EvaluateAsync<bool>("e => e.scrollWidth <= e.clientWidth + 1")).Should().BeTrue();
        await CheckoutScreenshot("twenty-fields-confirm", width);

        await Field(20).FillAsync("");
        await Field(20).BlurAsync();
        await Expect(confirm).ToBeDisabledAsync();
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        state.FinancialCalls.Should().Be(0);
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    public async Task CheckoutControls_ActiveLayout_RequiredPaymentAndCancelledHold(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(app.Urls.Single() + "/pos-settings-fixture");
        await Page.GetByRole(AriaRole.Switch, new() { Name = "Enable table selection", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Switch, new() { Name = "Require a table at payment", Exact = true }).ClickAsync();
        for (var index = 0; index < 2; index++)
        {
            await Page.GetByRole(AriaRole.Button, new() { Name = "New layout", Exact = true }).ClickAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Add table", Exact = true }).ClickAsync();
        }
        await Page.GetByRole(AriaRole.Button, new() { Name = "Use this layout", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Wait(() => Volatile.Read(ref state.SettingsSaves) == 1);
        state.CheckoutSettings.TableLayouts.Should().HaveCount(2);
        state.CheckoutSettings.ActiveTableLayout!.Name.Should().Be("Dining room 2");
        state.CheckoutSettings.TableRequired.Should().BeTrue();
        await Page.GotoAsync(app.Urls.Single() + "/cashier-scanner-fixture");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Hold sale", Exact = true }).ClickAsync();
        var picker = Page.GetByRole(AriaRole.Dialog, new() { Name = "Select table", Exact = true });
        await picker.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(picker).ToBeHiddenAsync();
        state.HeldCart.Should().BeNull();
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        await Page.GetByTestId("pos-checkout").ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Exact amount", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("pos-confirm-payment")).ToBeDisabledAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Select table", Exact = true }).ClickAsync();
        await Expect(picker.GetByText("Dining room 2", new() { Exact = true })).ToBeVisibleAsync();
        await picker.GetByRole(AriaRole.Button, new() { Name = "Table 1, 4 seats", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("pos-confirm-payment")).ToBeEnabledAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "No table", Exact = true })).ToHaveCountAsync(0);
        state.FinancialCalls.Should().Be(0);
        await CheckoutScreenshot("required-table-payment", width);
    }

    private async Task CheckoutScreenshot(string name, int width)
    {
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
        foreach (var dialog in await Page.GetByRole(AriaRole.Dialog).AllAsync())
            if (await dialog.IsVisibleAsync())
                (await dialog.EvaluateAsync<bool>("e => {const r=e.getBoundingClientRect(); return r.left>=0 && r.right<=innerWidth && r.top>=0 && r.bottom<=innerHeight+1;}")).Should().BeTrue();
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "pos-polish", $"{name}-{width}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true, Animations = ScreenshotAnimations.Disabled });
        TestContext.AddTestAttachment(path);
    }
}

public sealed partial class PairingFixtureState
{
    public PosCheckoutSettings CheckoutSettings = new();
    public PosCartResponse? HeldCart;
    public Guid CustomerIdentityId { get; } = Guid.NewGuid();
    public Guid CustomerCredentialId { get; } = Guid.NewGuid();
    public bool DenyCustomerCreate;
    public int SettingsSaves;
    public int IdentityCreates;
    public int CredentialCreates;
}

public sealed class PosSettingsFixtureSurface : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<XFramework.Portal.Features.POS.Pages.Settings>(0); builder.CloseComponent();
        builder.OpenComponent<XfContainerPortalHost>(1); builder.CloseComponent();
        builder.OpenComponent<BbOverlayPortalHost>(2); builder.CloseComponent();
    }
}
