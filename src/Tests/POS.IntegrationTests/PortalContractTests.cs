using System.Text.RegularExpressions;
using XFramework.TestInfrastructure;

namespace POS.IntegrationTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.POS)]
[Category(TestCategories.PortalContract)]
public sealed class PortalContractTests
{
    private static readonly string[] PosPages =
    [
        "Cashier.razor",
        "Registers.razor",
        "RegisterDetail.razor",
        "Sales.razor",
        "SaleDetail.razor",
        "Returns.razor",
        "ReturnDetail.razor"
    ];

    private static readonly string[] PosTabularPages =
    [
        "Registers.razor",
        "Sales.razor",
        "Returns.razor"
    ];

    [Test]
    public void PosPages_TabularSurfaces_UseFilteredBlazorBlueprintDataGrids()
    {
        var pagesRoot = GetPosPagesRoot();

        foreach (var page in PosTabularPages)
        {
            var text = File.ReadAllText(Path.Combine(pagesRoot, page));

            text.Should().NotContain("<table", $"{page} should use BlazorBlueprint data grids instead of raw tables");
            text.Should().Contain("<BbDataGrid", $"{page} should use BbDataGrid for list/report tabular records");
            text.Should().Contain("Filterable=\"true\"", $"{page} should expose native filtering on useful columns");
            text.Should().Contain("<EmptyTemplate>", $"{page} should expose explicit grid empty states");
            text.Should().NotMatchRegex(
                @"<BbDataGridTemplateColumn\b[^>]*Title=""Actions""[^>]*Filterable=""true""",
                $"{page} should not expose filters on command/action columns");
        }
    }

    [Test]
    public void PosCashier_UsesTouchFirstBlueprintDataViewsAndLayout()
    {
        var pagesRoot = GetPosPagesRoot();
        var cashier = File.ReadAllText(Path.Combine(pagesRoot, "Cashier.razor"));

        cashier.Should().NotContain("<table", "cashier touch surfaces should not use raw tables");
        cashier.Should().NotContain("<BbDataGrid", "cashier product/cart surfaces are touch workflows, not tabular reports");
        cashier.Should().Contain("<BbDataView TItem=\"PosCatalogItemResponse\"");
        cashier.Should().Contain("<BbDataView TItem=\"CartLine\"");
        cashier.Should().Contain("<BbDataView TItem=\"PosCartSummaryResponse\"");
        cashier.Should().Contain("Layout=\"DataViewLayout.Grid\"");
        cashier.Should().Contain("GridColumnMinWidth=\"12rem\"");
        cashier.Should().Contain("<BbRadioGroup TValue=\"PosPaymentMethod\" @bind-Value=\"_paymentMethod\"");
        cashier.Should().Contain("<BbDialog Open=\"@_saleDetailsOpen\"");
        cashier.Should().Contain("<BbDialog Open=\"@_heldCartsOpen\"");
        cashier.Should().Contain("ButtonSize.Large");
        cashier.Should().Contain("ButtonSize.IconLarge");
        cashier.Should().Contain("pos-cashier-shell");
        cashier.Should().Contain("pos-product-tile");
        cashier.Should().Contain("pos-cart-panel");
        cashier.Should().Contain("pos-total-bar");
        cashier.Should().Contain("CategoryId = _selectedCategoryId");
        cashier.Should().Contain("RegisterId = TryResolveSelectedRegisterId()");
        cashier.Should().Contain("DataContext.Query<ProductCategory>()");
        cashier.Should().NotContain("grid-cols-[");
    }

    [Test]
    public void PosCashier_CssDefinesTouchLayoutContracts()
    {
        var css = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "wwwroot",
            "css",
            "app.css"));

        var requiredClasses = new[]
        {
            "pos-cashier-shell",
            "pos-catalog-panel",
            "pos-cart-panel",
            "pos-checkout-panel",
            "pos-product-grid",
            "pos-product-tile",
            "pos-cart-line",
            "pos-total-bar",
            "pos-checkout-action"
        };

        foreach (var cssClass in requiredClasses)
            css.Should().Contain($".{cssClass}", $"{cssClass} is part of the cashier touch layout contract");
    }

    [Test]
    public void PosPages_Mutations_UseGeneratedServiceWrapper()
    {
        var pagesRoot = GetPosPagesRoot();
        var cashier = File.ReadAllText(Path.Combine(pagesRoot, "Cashier.razor"));
        var registers = File.ReadAllText(Path.Combine(pagesRoot, "Registers.razor"));
        var registerDetail = File.ReadAllText(Path.Combine(pagesRoot, "RegisterDetail.razor"));
        var sales = File.ReadAllText(Path.Combine(pagesRoot, "Sales.razor"));
        var saleDetail = File.ReadAllText(Path.Combine(pagesRoot, "SaleDetail.razor"));
        var returns = File.ReadAllText(Path.Combine(pagesRoot, "Returns.razor"));
        var returnDetail = File.ReadAllText(Path.Combine(pagesRoot, "ReturnDetail.razor"));

        foreach (var text in new[] { cashier, registers, registerDetail, sales, saleDetail, returns, returnDetail })
            text.Should().Contain("IPOSServiceWrapper POS");

        cashier.Should().Contain("POS.CheckoutPosSale(");
        cashier.Should().Contain("POS.CreatePosCart(");
        cashier.Should().Contain("POS.UpdatePosCart(");
        cashier.Should().Contain("POS.SuspendPosCart(");
        cashier.Should().Contain("POS.ResumePosCart(");
        cashier.Should().Contain("POS.CancelPosCart(");
        cashier.Should().Contain("POS.CheckoutPosCart(");
        cashier.Should().Contain("POS.SearchPosCatalog(");
        cashier.Should().Contain("POS.RetryPosSalePayment(");
        cashier.Should().Contain("NewSaleIdempotencyKey()");
        cashier.Should().Contain("_cashTenderedAmount < Total");
        registers.Should().Contain("POS.CreatePosRegister(");
        registerDetail.Should().Contain("POS.GetPosRegister(");
        registerDetail.Should().Contain("POS.UpdatePosRegister(");
        sales.Should().Contain("POS.CancelPosSale(");
        sales.Should().Contain("POS.RetryPosSaleFulfillment(");
        saleDetail.Should().Contain("POS.GetPosSale(");
        saleDetail.Should().Contain("POS.RetryPosSalePayment(");
        returns.Should().Contain("POS.CreatePosReturn(");
        returns.Should().Contain("NewReturnIdempotencyKey()");
        returns.Should().Contain("IdempotencyKey = _returnIdempotencyKey");
        returnDetail.Should().Contain("POS.GetPosReturn(");
        returnDetail.Should().Contain("POS.RetryPosReturn(");

        var offenders = PosPages
            .Select(page => new
            {
                Page = page,
                Text = File.ReadAllText(Path.Combine(pagesRoot, page))
            })
            .SelectMany(item => FindDirectPosMutations(item.Page, item.Text))
            .ToArray();

        offenders.Should().BeEmpty("POS business mutations must go through IPOSServiceWrapper endpoints");
    }

    [Test]
    public void PosPages_UseExactTenantSubFeatureGates()
    {
        var pagesRoot = GetPosPagesRoot();
        var layoutRoot = Path.Combine(
            FindRepositoryRoot().FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Layout");
        var navMenu = File.ReadAllText(Path.Combine(layoutRoot, "NavMenu.razor"));

        foreach (var page in PosPages)
        {
            var text = File.ReadAllText(Path.Combine(pagesRoot, page));

            text.Should().Contain("_moduleEnabled");
            text.Should().Contain("ModuleUnavailable");
            text.Should().Contain("TenantModuleFeatureKeys.Pos");
            text.Should().NotContain("|| ModuleNavigation.IsFeatureEnabled(TenantModuleFeatureKeys.Pos)");
        }

        navMenu.Should().Contain("Href=\"/pos/cashier\"");
        navMenu.Should().Contain("ModuleNavigation.IsFeatureEnabled(TenantModuleFeatureKeys.Pos, \"sales\")");
        navMenu.Should().Contain("ModuleNavigation.IsFeatureEnabled(TenantModuleFeatureKeys.Pos, \"registers\")");
        navMenu.Should().Contain("ModuleNavigation.IsFeatureEnabled(TenantModuleFeatureKeys.Pos, \"returns\")");
        navMenu.Should().NotContain("@if (ModuleNavigation.IsFeatureEnabled(TenantModuleFeatureKeys.Pos))");

        File.ReadAllText(Path.Combine(pagesRoot, "Cashier.razor"))
            .Should().Contain("_moduleEnabled = ModuleNavigation.IsFeatureEnabled(TenantModuleFeatureKeys.Pos, \"sales\");");
        File.ReadAllText(Path.Combine(pagesRoot, "Registers.razor"))
            .Should().Contain("_moduleEnabled = ModuleNavigation.IsFeatureEnabled(TenantModuleFeatureKeys.Pos, \"registers\");");
        File.ReadAllText(Path.Combine(pagesRoot, "Sales.razor"))
            .Should().Contain("_moduleEnabled = ModuleNavigation.IsFeatureEnabled(TenantModuleFeatureKeys.Pos, \"sales\");");
        File.ReadAllText(Path.Combine(pagesRoot, "Returns.razor"))
            .Should().Contain("_moduleEnabled = ModuleNavigation.IsFeatureEnabled(TenantModuleFeatureKeys.Pos, \"returns\");");
    }

    [Test]
    public void RegisterSetup_UsesEntityPickersInsteadOfRawGuidEntry()
    {
        var pagesRoot = GetPosPagesRoot();
        var registerList = File.ReadAllText(Path.Combine(pagesRoot, "Registers.razor"));
        var registerDetail = File.ReadAllText(Path.Combine(pagesRoot, "RegisterDetail.razor"));

        foreach (var text in new[] { registerList, registerDetail })
        {
            text.Should().Contain("XfEntityPicker TItem=\"IdentityCredential\"");
            text.Should().Contain("XfEntityPicker TItem=\"Wallet\"");
            text.Should().Contain("XfEntityPicker TItem=\"Wallets.Domain.Shared.Contracts.WalletType\"");
            text.Should().Contain("XfEntityPicker TItem=\"Wallets.Domain.Shared.Contracts.CurrencyType\"");
            text.Should().Contain("XfEntityPicker TItem=\"Warehouse\"");
            text.Should().Contain("XfEntityPicker TItem=\"InventoryLocation\"");
            text.Should().NotContain("TValue=\"Guid\"");
            text.Should().NotContain("raw GUID");
        }
    }

    [Test]
    public void RegisterList_OpensDedicatedDetailWorkflow()
    {
        var registerList = File.ReadAllText(Path.Combine(GetPosPagesRoot(), "Registers.razor"));

        registerList.Should().Contain("OnRowClick=\"@((PosRegisterResponse item) => OpenRegister(item.Id))\"");
        registerList.Should().Contain("Navigation.NavigateTo($\"/pos/registers/{id}\")");
        registerList.Should().Contain("Title=\"Actions\"");
        registerList.Should().Contain("Edit register {GetRegisterLabel(item)}");
        registerList.Should().NotContain("UpdatePosRegisterRequest");
        registerList.Should().NotContain("POS.UpdatePosRegister(");
    }

    [Test]
    public void RegisterDetail_UsesWrapperBackedEditWithReferenceValidation()
    {
        var detail = File.ReadAllText(Path.Combine(GetPosPagesRoot(), "RegisterDetail.razor"));

        detail.Should().Contain("@page \"/pos/registers/{Id:guid}\"");
        detail.Should().Contain("data-testid=\"pos-register-detail\"");
        detail.Should().Contain("POS.GetPosRegister(new GetPosRegisterRequest");
        detail.Should().Contain("POS.UpdatePosRegister(request)");
        detail.Should().Contain("wallet.CredentialId != merchantCredentialId");
        detail.Should().Contain("walletType.CurrencyTypeId is Guid walletTypeCurrencyId");
        detail.Should().Contain("item.Id == locationId && item.WarehouseId == warehouseId");
        detail.Should().Contain("<BbAlertTitle>Register setup incomplete</BbAlertTitle>");
        detail.Should().Contain("<BbAlertDescription>@_validationMessage</BbAlertDescription>");
        detail.Should().Contain("_validationMessage = validationMessage;");
        detail.Should().Contain("Label=\"Enabled\"");
        detail.Should().NotContain("DataContext.Update");
    }

    [Test]
    public void PosPages_UseWrapperReadsWherePosContractsExist()
    {
        var pagesRoot = GetPosPagesRoot();
        var registers = File.ReadAllText(Path.Combine(pagesRoot, "Registers.razor"));
        var sales = File.ReadAllText(Path.Combine(pagesRoot, "Sales.razor"));
        var returns = File.ReadAllText(Path.Combine(pagesRoot, "Returns.razor"));

        registers.Should().Contain("POS.SearchPosRegisters(new SearchPosRegistersRequest");
        registers.Should().NotContain("DataContext.Query<PosRegister>()");

        sales.Should().Contain("POS.SearchPosSales(new SearchPosSalesRequest");
        sales.Should().NotContain("DataContext.Query<PosSale>()");

        returns.Should().Contain("POS.SearchPosSales(new SearchPosSalesRequest");
        returns.Should().Contain("POS.SearchPosReturns(new SearchPosReturnsRequest");
        returns.Should().Contain("POS.GetPosSale(new GetPosSaleRequest");
        returns.Should().Contain("POS.GetPosReturn(new GetPosReturnRequest");
        returns.Should().NotContain("DataContext.Query<PosReturn>()");
        returns.Should().NotContain("DataContext.Query<PosSaleLine>()");
    }

    [Test]
    public void Returns_SubtractsPriorReturnsAndPreviewsAllocatedRefund()
    {
        var returns = File.ReadAllText(Path.Combine(GetPosPagesRoot(), "Returns.razor"));

        returns.Should().Contain("PreviouslyReturnedQuantity");
        returns.Should().Contain("RemainingQuantity");
        returns.Should().Contain("BuildSaleRefundAllocations");
        returns.Should().Contain("OriginalRefundAmount - PreviouslyReturnedRefundAmount");
        returns.Should().Contain("Math.Clamp(line.ReturnQuantity + delta, 0, line.RemainingQuantity)");
        returns.Should().Contain("SaleId = saleId");
        returns.Should().Contain("for (var page = 1; ; page++)");
    }

    [Test]
    public void PosPages_UseBlueprintControlsAndConfirmDestructiveActions()
    {
        var pagesRoot = GetPosPagesRoot();
        var cashier = File.ReadAllText(Path.Combine(pagesRoot, "Cashier.razor"));
        var sales = File.ReadAllText(Path.Combine(pagesRoot, "Sales.razor"));
        var returns = File.ReadAllText(Path.Combine(pagesRoot, "Returns.razor"));

        cashier.Should().Contain("<BbRadioGroup TValue=\"PosPaymentMethod\" @bind-Value=\"_paymentMethod\"");
        cashier.Should().Contain("<BbCurrencyInput Id=\"pos-cash-amount\" @bind-Value=\"_cashTenderedAmount\" AriaLabel=\"Cash received\"");
        cashier.Should().Contain("CashTenderedAmount = IsCashPayment ? _cashTenderedAmount : null");
        cashier.Should().Contain("data-testid=\"pos-receipt-cash\"");
        cashier.Should().Contain("IsMerchantCustomer");
        cashier.Should().Contain("Customer wallet must be different from the register merchant wallet.");
        cashier.Should().Contain("the sale total must be greater than zero.");
        returns.Should().Contain("data-testid=\"pos-original-refund-method\"");
        returns.Should().Contain("_refundMethod = response.Response.PaymentMethod;");
        returns.Should().Contain("original captured payment method and account");
        returns.Should().NotContain("RefundMethodValue");
        cashier.Should().NotContain("grid-cols-[");
        returns.Should().NotContain("@if (_refundMethod == PosPaymentMethod.CashDrawer)");
        cashier.Should().NotContain("@if (_paymentMethod == PosPaymentMethod.CashDrawer)");

        cashier.Should().Contain("ConfirmClearCurrentCart");
        cashier.Should().Contain("Reason = \"Cleared from Portal\"");
        cashier.Should().Contain("if (!response.IsSuccess)");
        cashier.Should().Contain("Cancel held sale");
        cashier.Should().Contain("Held sales");
        sales.Should().Contain("Cancel POS Sale");
        (cashier + sales).Should().Contain("new ConfirmDialogOptions { Destructive = true }");
    }

    [Test]
    public void Cashier_ReplacesCartCollectionAfterMutationsSoDataViewRefreshes()
    {
        var cashier = File.ReadAllText(Path.Combine(GetPosPagesRoot(), "Cashier.razor"));

        cashier.Should().Contain("private List<CartLine> _cart = [];");
        cashier.Should().Contain("_cart = [.. _cart];");
        cashier.Should().Contain("_cart = _cart.Where(item => !ReferenceEquals(item, line)).ToList();");
        cashier.Should().NotContain("private readonly List<CartLine> _cart");
    }

    [Test]
    public void PosEntityPickers_DefineAdvancedSearchColumnsAndScope()
    {
        var pagesRoot = GetPosPagesRoot();
        var registers = File.ReadAllText(Path.Combine(pagesRoot, "Registers.razor"));
        var registerDetail = File.ReadAllText(Path.Combine(pagesRoot, "RegisterDetail.razor"));
        var cashier = File.ReadAllText(Path.Combine(pagesRoot, "Cashier.razor"));
        var returns = File.ReadAllText(Path.Combine(pagesRoot, "Returns.razor"));

        Regex.Matches(registers, "AdvancedColumns=\"@").Count.Should().BeGreaterThanOrEqualTo(6);
        Regex.Matches(registers, "AdvancedSearchScope=").Count.Should().BeGreaterThanOrEqualTo(6);
        Regex.Matches(registerDetail, "AdvancedColumns=\"@").Count.Should().BeGreaterThanOrEqualTo(6);
        Regex.Matches(registerDetail, "AdvancedSearchScope=").Count.Should().BeGreaterThanOrEqualTo(6);
        Regex.Matches(cashier, "AdvancedColumns=\"@").Count.Should().BeGreaterThanOrEqualTo(2);
        Regex.Matches(cashier, "AdvancedSearchScope=").Count.Should().BeGreaterThanOrEqualTo(2);
        Regex.Matches(returns, "AdvancedColumns=\"@").Count.Should().BeGreaterThanOrEqualTo(1);
        Regex.Matches(returns, "AdvancedSearchScope=").Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Test]
    public void PosCashier_SuspendedCartUiAndActions_RequireCartsFeature()
    {
        var text = File.ReadAllText(Path.Combine(GetPosPagesRoot(), "Cashier.razor"));

        text.Should().Contain("_cartsEnabled = ModuleNavigation.IsFeatureEnabled(TenantModuleFeatureKeys.Pos, \"carts\");");
        text.Should().Contain("@if (_cartsEnabled)");
        text.Should().Contain("POS.SearchPosCarts(new SearchPosCartsRequest");
        text.Should().Contain("POS.SuspendPosCart(new SuspendPosCartRequest");
        text.Should().Contain("POS.ResumePosCart(new ResumePosCartRequest");
        text.Should().Contain("POS.CancelPosCart(new CancelPosCartRequest");
        text.Should().Contain("Search = string.IsNullOrWhiteSpace(_heldCartSearch)");
        text.Should().Contain("Page = _heldCartPage");
        text.Should().Contain("PageSize = HeldCartPageSize");
        text.Should().Contain("ShowPagination=\"false\"");
    }

    [Test]
    public void PosReferencePickers_RequestServerSearchInsteadOfRelyingOnInitialCaps()
    {
        var pagesRoot = GetPosPagesRoot();
        var cashier = File.ReadAllText(Path.Combine(pagesRoot, "Cashier.razor"));
        var registers = File.ReadAllText(Path.Combine(pagesRoot, "Registers.razor"));
        var detail = File.ReadAllText(Path.Combine(pagesRoot, "RegisterDetail.razor"));
        var picker = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "src", "Presentation", "XFramework.Portal.Shared", "Components", "XfEntityPicker.razor"));

        picker.Should().Contain("[Parameter] public EventCallback<string> SearchRequested");
        picker.Should().Contain("await Task.Delay(250, _searchDebounce.Token)");
        cashier.Should().Contain("SearchRequested=\"@SearchRegisterOptions\"");
        cashier.Should().Contain("SearchRequested=\"@SearchCredentialOptions\"");
        cashier.Should().Contain("POS.SearchPosRegisters(new SearchPosRegistersRequest");
        Regex.Matches(registers, "SearchRequested=\"@").Count.Should().BeGreaterThanOrEqualTo(6);
        Regex.Matches(detail, "SearchRequested=\"@").Count.Should().BeGreaterThanOrEqualTo(6);
        registers.Should().Contain("Insert(0, selected)");
        detail.Should().Contain("Insert(0, selected)");
    }

    [Test]
    public void PosCashier_PendingPayment_FreezesPayloadAndRetriesPersistedSale()
    {
        var cashier = File.ReadAllText(Path.Combine(GetPosPagesRoot(), "Cashier.razor"));

        cashier.Should().Contain("private bool HasPendingPayment => _lastReceipt?.Status == PosSaleStatus.PaymentPending;");
        cashier.Should().Contain("private bool HasTerminalCheckoutFailure => _lastReceipt?.Status is");
        cashier.Should().Contain("private bool IsInteractionLocked => IsBusy || HasPendingPayment || HasTerminalCheckoutFailure;");
        cashier.Should().Contain("if (HasPendingPayment)");
        cashier.Should().Contain("await RetryPendingPayment();");
        cashier.Should().Contain("POS.RetryPosSalePayment(new RetryPosSalePaymentRequest");
        cashier.Should().Contain("SaleId = pendingSale.Id");
        cashier.Should().Contain("Payment result is unknown. Retry to reuse the original payment reference.");
        cashier.Should().Contain("This sale cannot be retried. Start a new sale to use a new payment reference.");
        cashier.Should().Contain("!HasTerminalCheckoutFailure");
        cashier.Should().Contain("Disabled=\"@IsInteractionLocked\"");
        Regex.Matches(cashier, @"if \(_lastReceipt\?\.Status == PosSaleStatus\.Completed\)\s*\{\s*ClearCurrentCart\(\);")
            .Count.Should().BeGreaterThanOrEqualTo(2, "both direct and held-cart checkout must retain ambiguous payments");
    }

    [Test]
    public void PosCashier_CompletedCheckout_RefreshesCatalogAfterInteractionUnlocks()
    {
        var cashier = File.ReadAllText(Path.Combine(GetPosPagesRoot(), "Cashier.razor"));

        cashier.Should().Contain("var refreshCatalogAfterCheckout = false;");
        cashier.Should().Contain("refreshCatalogAfterCheckout = true;");
        Regex.Matches(cashier,
                @"finally\s*\{\s*_checkingOut = false;\s*if \(refreshCatalogAfterCheckout\)\s*await SearchCatalog\(false, false\);")
            .Count.Should().Be(2, "both checkout and pending-payment recovery must refresh displayed availability");
    }

    [Test]
    public void PosSalesAndReturns_UseServerPagingAndDedicatedDetailRoutes()
    {
        var pagesRoot = GetPosPagesRoot();
        var sales = File.ReadAllText(Path.Combine(pagesRoot, "Sales.razor"));
        var saleDetail = File.ReadAllText(Path.Combine(pagesRoot, "SaleDetail.razor"));
        var returns = File.ReadAllText(Path.Combine(pagesRoot, "Returns.razor"));
        var returnDetail = File.ReadAllText(Path.Combine(pagesRoot, "ReturnDetail.razor"));

        sales.Should().Contain("Page = _page");
        sales.Should().Contain("PageSize = PageSize");
        sales.Should().Contain("Search = string.IsNullOrWhiteSpace(_search)");
        sales.Should().Contain("Navigation.NavigateTo($\"/pos/sales/{saleId}\")");
        saleDetail.Should().Contain("@page \"/pos/sales/{Id:guid}\"");
        saleDetail.Should().Contain("data-testid=\"pos-sale-detail\"");

        returns.Should().Contain("Page = _returnPage");
        returns.Should().Contain("PageSize = PageSize");
        returns.Should().Contain("Search = string.IsNullOrWhiteSpace(_returnSearch)");
        returns.Should().Contain("Search = string.IsNullOrWhiteSpace(_saleSearch)");
        returns.Should().Contain("SearchRequested=\"@SearchCompletedSalesFromPicker\"");
        returns.Should().Contain("Navigation.NavigateTo($\"/pos/returns/{returnId}\")");
        returnDetail.Should().Contain("@page \"/pos/returns/{Id:guid}\"");
        returnDetail.Should().Contain("data-testid=\"pos-return-detail\"");
    }

    [Test]
    public void PosSaleAndReturnDetails_ShowAuditableLabelsCurrencyAndReceiptPrinting()
    {
        var pagesRoot = GetPosPagesRoot();
        var saleDetail = File.ReadAllText(Path.Combine(pagesRoot, "SaleDetail.razor"));
        var returnDetail = File.ReadAllText(Path.Combine(pagesRoot, "ReturnDetail.razor"));
        var printStyles = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "src", "Presentation", "XFramework.Portal", "wwwroot", "css", "app.css"));

        saleDetail.Should().Contain("Print receipt");
        saleDetail.Should().Contain("JSRuntime.InvokeVoidAsync(\"print\")");
        saleDetail.Should().Contain("Refunded Amount");
        saleDetail.Should().Contain("item.RefundedAmount");
        saleDetail.Should().Contain("_registerLabel");
        saleDetail.Should().Contain("_cashierLabel");
        saleDetail.Should().Contain("_customerLabel");
        saleDetail.Should().Contain("FormatMoney(_sale.TotalAmount)");
        printStyles.Should().Contain(".pos-sale-receipt .overflow-auto");
        printStyles.Should().Contain("overflow: visible !important;");
        printStyles.Should().Contain("table-layout: fixed !important;");
        printStyles.Should().Contain("background: #fff !important;");
        printStyles.Should().Contain("break-inside: avoid;");
        returnDetail.Should().Contain("_registerLabel");
        returnDetail.Should().Contain("_cashierLabel");
        returnDetail.Should().Contain("_customerLabel");
        returnDetail.Should().Contain("FormatMoney(_return.TotalRefundAmount)");
    }

    [Test]
    public void PosRegisterList_PreservesLoadFailuresInsteadOfRenderingAnEmptyResult()
    {
        var registers = File.ReadAllText(Path.Combine(GetPosPagesRoot(), "Registers.razor"));

        registers.Should().Contain("Registers could not load");
        registers.Should().Contain("Register data is unavailable");
        registers.Should().Contain("if (!string.IsNullOrWhiteSpace(_loadError))");
        registers.Should().Contain("_loadError = response.Message ?? \"The POS service did not return register data.\"");
    }

    [Test]
    public void PosCashier_RequiresRegisterCurrencyBeforeRenderingMoneyControls()
    {
        var cashier = File.ReadAllText(Path.Combine(GetPosPagesRoot(), "Cashier.razor"));

        cashier.Should().Contain("Title=\"Select a register\"");
        cashier.Should().Contain("Register currency unavailable");
        cashier.Should().Contain("RegisterCurrency is { } currency ? $\"{currency} {value:N2}\" : \"—\"");
        cashier.Should().NotContain("RegisterCurrency ?? \"XXX\"");
    }

    private static IEnumerable<string> FindDirectPosMutations(string page, string text)
    {
        var entities = new[] { "PosRegister", "PosCart", "PosCartLine", "PosSale", "PosSaleLine", "PosPayment", "PosReturn", "PosReturnLine" };
        var operations = new[] { "Add", "Update", "Remove" };

        foreach (var entity in entities)
        {
            foreach (var operation in operations)
            {
                var inlinePattern = $@"DataContext\.{operation}\s*\(\s*new\s+{Regex.Escape(entity)}\b";
                if (Regex.IsMatch(text, inlinePattern, RegexOptions.Multiline))
                    yield return $"{page} directly {operation.ToLowerInvariant()}s {entity}";

                var genericPattern = $@"DataContext\.{operation}\s*<\s*{Regex.Escape(entity)}\s*>";
                if (Regex.IsMatch(text, genericPattern, RegexOptions.Multiline))
                    yield return $"{page} directly {operation.ToLowerInvariant()}s {entity}";
            }
        }
    }

    private static string GetPosPagesRoot()
    {
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal.Features.POS",
            "Pages");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XFramework.slnx")))
                return current;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate XFramework repository root.");
    }
}
