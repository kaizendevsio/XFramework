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
        "Sales.razor",
        "Returns.razor"
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
        var sales = File.ReadAllText(Path.Combine(pagesRoot, "Sales.razor"));
        var returns = File.ReadAllText(Path.Combine(pagesRoot, "Returns.razor"));

        foreach (var text in new[] { cashier, registers, sales, returns })
            text.Should().Contain("IPOSServiceWrapper POS");

        cashier.Should().Contain("POS.CheckoutPosSale(");
        cashier.Should().Contain("POS.CreatePosCart(");
        cashier.Should().Contain("POS.UpdatePosCart(");
        cashier.Should().Contain("POS.SuspendPosCart(");
        cashier.Should().Contain("POS.ResumePosCart(");
        cashier.Should().Contain("POS.CancelPosCart(");
        cashier.Should().Contain("POS.CheckoutPosCart(");
        cashier.Should().Contain("POS.SearchPosCatalog(");
        cashier.Should().Contain("NewSaleIdempotencyKey()");
        cashier.Should().Contain("_cashTenderedAmount < Total");
        registers.Should().Contain("POS.CreatePosRegister(");
        sales.Should().Contain("POS.CancelPosSale(");
        sales.Should().Contain("POS.RetryPosSaleFulfillment(");
        returns.Should().Contain("POS.CreatePosReturn(");
        returns.Should().Contain("NewReturnIdempotencyKey()");
        returns.Should().Contain("IdempotencyKey = _returnIdempotencyKey");

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
        var text = File.ReadAllText(Path.Combine(GetPosPagesRoot(), "Registers.razor"));

        text.Should().Contain("XfEntityPicker TItem=\"IdentityCredential\"");
        text.Should().Contain("XfEntityPicker TItem=\"Wallet\"");
        text.Should().Contain("XfEntityPicker TItem=\"Wallets.Domain.Shared.Contracts.WalletType\"");
        text.Should().Contain("XfEntityPicker TItem=\"Wallets.Domain.Shared.Contracts.CurrencyType\"");
        text.Should().Contain("XfEntityPicker TItem=\"Warehouse\"");
        text.Should().Contain("XfEntityPicker TItem=\"InventoryLocation\"");
        text.Should().NotContain("TValue=\"Guid\"");
        text.Should().NotContain("raw GUID");
    }

    [Test]
    public void PosPages_UseWrapperReadsWherePosContractsExist()
    {
        var pagesRoot = GetPosPagesRoot();
        var sales = File.ReadAllText(Path.Combine(pagesRoot, "Sales.razor"));
        var returns = File.ReadAllText(Path.Combine(pagesRoot, "Returns.razor"));

        sales.Should().Contain("POS.SearchPosSales(new SearchPosSalesRequest");
        sales.Should().NotContain("DataContext.Query<PosSale>()");

        returns.Should().Contain("POS.SearchPosSales(new SearchPosSalesRequest");
        returns.Should().Contain("POS.SearchPosReturns(new SearchPosReturnsRequest");
        returns.Should().Contain("POS.GetPosSale(new GetPosSaleRequest");
        returns.Should().NotContain("DataContext.Query<PosReturn>()");
        returns.Should().NotContain("DataContext.Query<PosSaleLine>()");
    }

    [Test]
    public void PosPages_UseBlueprintControlsAndConfirmDestructiveActions()
    {
        var pagesRoot = GetPosPagesRoot();
        var cashier = File.ReadAllText(Path.Combine(pagesRoot, "Cashier.razor"));
        var sales = File.ReadAllText(Path.Combine(pagesRoot, "Sales.razor"));
        var returns = File.ReadAllText(Path.Combine(pagesRoot, "Returns.razor"));

        cashier.Should().Contain("<BbRadioGroup TValue=\"PosPaymentMethod\" @bind-Value=\"_paymentMethod\"");
        cashier.Should().Contain("BbFormFieldCurrencyInput @bind-Value=\"_cashTenderedAmount\"");
        returns.Should().Contain("<BbFormFieldSelect TValue=\"string\" @bind-Value=\"RefundMethodValue\" Label=\"Refund Method\">");
        cashier.Should().NotContain("grid-cols-[");
        returns.Should().NotContain("@if (_refundMethod == PosPaymentMethod.CashDrawer)");
        cashier.Should().NotContain("@if (_paymentMethod == PosPaymentMethod.CashDrawer)");

        cashier.Should().Contain("ConfirmClearCurrentCart");
        cashier.Should().Contain("Cancel Suspended Cart");
        cashier.Should().Contain("Held Carts");
        sales.Should().Contain("Cancel POS Sale");
        (cashier + sales).Should().Contain("new ConfirmDialogOptions { Destructive = true }");
    }

    [Test]
    public void PosEntityPickers_DefineAdvancedSearchColumnsAndScope()
    {
        var pagesRoot = GetPosPagesRoot();
        var registers = File.ReadAllText(Path.Combine(pagesRoot, "Registers.razor"));
        var cashier = File.ReadAllText(Path.Combine(pagesRoot, "Cashier.razor"));
        var returns = File.ReadAllText(Path.Combine(pagesRoot, "Returns.razor"));

        Regex.Matches(registers, "AdvancedColumns=\"@").Count.Should().BeGreaterThanOrEqualTo(6);
        Regex.Matches(registers, "AdvancedSearchScope=").Count.Should().BeGreaterThanOrEqualTo(6);
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
            "XFramework.Portal",
            "Components",
            "Pages",
            "POS");
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
