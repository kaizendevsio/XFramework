using XFramework.TestInfrastructure;
using System.Text.RegularExpressions;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.Inventario)]
[Category(TestCategories.PortalContract)]
public sealed class PortalContractTests
{
    private static readonly string[] BusinessWorkflowEntities =
    [
        "Product",
        "InventoryReorderRule",
        "Warehouse",
        "InventoryLocation",
        "InventoryLot",
        "ProductVariationType",
        "ProductVariation",
        "ProductTransaction",
        "StockBalance",
        "InventoryMovement",
        "Reservation",
        "ReservationAllocation",
        "Supplier",
        "PurchaseOrder",
        "PurchaseOrderLine",
        "ReceivingDocument",
        "ReceivingLine"
    ];

    private static readonly string[] InventarioListPagesRequiringFilteredDataGrids =
    [
        "Categories.razor",
        "Lots.razor",
        "Planning.razor",
        "Products.razor",
        "PurchaseOrders.razor",
        "Receiving.razor",
        "Reports.razor",
        "Reservations.razor",
        "Stock.razor",
        "Suppliers.razor",
        "Transactions.razor",
        "Warehouses.razor"
    ];

    private static readonly string[] InventarioDetailPagesRequiringFilteredDataGrids =
    [
        "CategoryDetail.razor",
        "WarehouseDetail.razor",
        "LocationDetail.razor",
        "LotDetail.razor",
        "StockBalanceDetail.razor",
        "ReceivingDocumentDetail.razor",
        "ReservationDetail.razor",
        "PurchaseOrderDetail.razor"
    ];

    private static readonly string[] InventarioWorkflowPagesRequiringEntityPickers =
    [
        "Products.razor",
        "ProductDetail.razor",
        "Lots.razor",
        "Stock.razor",
        "Receiving.razor",
        "PurchaseOrders.razor",
        "Reservations.razor",
        "Planning.razor",
        "Warehouses.razor"
    ];

    [Test]
    public void InventarioPages_BusinessWorkflowMutations_DoNotUseDirectRemoteDataContextMutation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Inventario");

        var offenders = Directory.EnumerateFiles(pagesRoot, "*.razor", SearchOption.AllDirectories)
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                return BusinessWorkflowEntities
                    .SelectMany(entity => FindDirectMutations(repositoryRoot, path, text, entity));
            })
            .ToArray();

        offenders.Should().BeEmpty("business workflow entities must go through IInventarioServiceWrapper endpoints");
    }

    [Test]
    public void ProductDetail_DependencyCreation_UsesEntityPickerOwnedDialogs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var productDetailPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Inventario",
            "ProductDetail.razor");

        var text = File.ReadAllText(productDetailPath);

        text.Should().Contain("<XfEntityPicker", "product workflows should select dependency entities through the shared picker");
        text.Should().NotContain("PrerequisiteCreateBlock", "dependency creation belongs to picker-owned dialogs, not embedded parent form sections");
        text.Should().NotContain("CreateWarehouseForWorkflow", "warehouse creation must be launched from the picker action, not inline in the stock form");
        text.Should().NotContain("CreateLocationForWorkflow", "location creation must be launched from the picker action, not inline in the stock form");
        text.Should().NotContain("WarehouseQuickForm", "quick prerequisite forms should not be embedded in product workflow dialogs");
        text.Should().NotContain("LocationQuickForm", "quick prerequisite forms should not be embedded in product workflow dialogs");
        text.Should().NotContain("Create the first warehouse without leaving this product workflow.");
        text.Should().NotContain("The selected warehouse has no locations yet.");
    }

    [Test]
    public void ProductDetail_EntityPickers_DefineAdvancedSearchColumnsAndFilters()
    {
        var repositoryRoot = FindRepositoryRoot();
        var productDetailPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Inventario",
            "ProductDetail.razor");
        var pickerPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Shared",
            "XfEntityPicker.razor");

        var productDetailText = File.ReadAllText(productDetailPath);
        var pickerText = File.ReadAllText(pickerPath);

        productDetailText.Should().Contain("AdvancedColumns=\"@WarehouseAdvancedColumns\"");
        productDetailText.Should().Contain("AdvancedFilters=\"@WarehouseAdvancedFilters\"");
        productDetailText.Should().Contain("AdvancedColumns=\"@LocationAdvancedColumns\"");
        productDetailText.Should().Contain("AdvancedFilters=\"@LocationAdvancedFilters\"");
        productDetailText.Should().Contain("AdvancedColumns=\"@LotAdvancedColumns\"");
        productDetailText.Should().Contain("AdvancedFilters=\"@LotAdvancedFilters\"");
        productDetailText.Should().Contain("AdvancedColumns=\"@SupplierAdvancedColumns\"");
        productDetailText.Should().Contain("AdvancedFilters=\"@SupplierAdvancedFilters\"");
        productDetailText.Should().Contain("AdvancedColumns=\"@PurchaseOrderAdvancedColumns\"");
        productDetailText.Should().Contain("AdvancedFilters=\"@PurchaseOrderAdvancedFilters\"");

        pickerText.Should().Contain("<BbDataGrid", "advanced search must use the shared BlazorBlueprint data grid instead of a custom table");
        pickerText.Should().Contain("BbDataGridTemplateColumn", "advanced search must render a multi-column finder, not the same single-column command list");
        pickerText.Should().Contain("SortBy", "advanced search columns should use native data grid sorting");
        pickerText.Should().Contain("xf-entity-picker-advanced-dialog", "advanced search dialogs should be wide enough for multi-column finder tables");
        pickerText.Should().Contain("Hover over a column header to reveal its filter.", "advanced search should make native column filters discoverable");
        pickerText.Should().Contain("ShowAdvancedColumnFilters", "advanced finder tables should support per-column filtering");
        pickerText.Should().Contain("FilterBy", "advanced finder tables should use native data grid column filtering");
        pickerText.Should().NotContain("xf-entity-picker-advanced-tools", "advanced search should not render a redundant search band above the data grid");
        pickerText.Should().NotContain("xf-entity-picker-advanced-filters", "advanced search should use native data grid filters instead of duplicate select filters above the grid");
        pickerText.Should().NotContain("xf-entity-picker-column-filter", "advanced finder tables should not render custom filter inputs");
        pickerText.Should().NotContain("<table class=\"xf-entity-picker-advanced-table\"", "advanced finder tables should not use a custom table implementation");
        pickerText.Should().NotContain("__all__", "filter sentinels must not leak into selected filter labels");
    }

    [Test]
    public void ProductPages_ProductWrites_UseInventarioServiceWrapper()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Inventario");
        var productsText = File.ReadAllText(Path.Combine(pagesRoot, "Products.razor"));
        var productDetailText = File.ReadAllText(Path.Combine(pagesRoot, "ProductDetail.razor"));

        productsText.Should().Contain("IInventarioServiceWrapper Inventario");
        productsText.Should().Contain("Inventario.CreateProduct(");
        productsText.Should().NotContain("DataContext.Add(new Product");

        productDetailText.Should().Contain("IInventarioServiceWrapper Inventario");
        productDetailText.Should().Contain("Inventario.UpdateProduct(");
        productDetailText.Should().NotContain("DataContext.Update");
    }

    [Test]
    public void ProductPages_CategorySelectors_UseEntityPicker()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Inventario");
        var productsText = File.ReadAllText(Path.Combine(pagesRoot, "Products.razor"));
        var productDetailText = File.ReadAllText(Path.Combine(pagesRoot, "ProductDetail.razor"));

        productsText.Should().Contain("XfEntityPicker TItem=\"ProductCategory\"");
        productsText.Should().Contain("AdvancedColumns=\"@CategoryAdvancedColumns\"");
        productsText.Should().NotContain("CategoryOptions");

        productDetailText.Should().Contain("XfEntityPicker TItem=\"ProductCategory\"");
        productDetailText.Should().Contain("AdvancedColumns=\"@CategoryAdvancedColumns\"");
        productDetailText.Should().NotContain("Options=\"@CategoryOptions\"");
    }

    [Test]
    public void ProductDetail_LongWorkflowDialogs_UseWideResponsiveLayoutsAndTypedInputs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var productDetailPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Inventario",
            "ProductDetail.razor");
        var text = File.ReadAllText(productDetailPath);

        text.Should().Contain("Class=\"xf-dialog-wide\"");
        text.Should().Contain("Class=\"xf-dialog-extra-wide\"");
        text.Should().Contain("@page \"/inventario/products/{Id:guid}/{Section}\"");
        text.Should().NotContain("xf-detail-layout");
        text.Should().NotContain("xf-detail-sidebar");
        text.Should().NotContain("xf-detail-nav");
        text.Should().NotContain("ProductSectionNav");
        text.Should().Contain("BbFormFieldNumericInput TValue=\"decimal\"");
        text.Should().Contain("BbFormFieldCurrencyInput");
        text.Should().Contain("BbFormFieldDatePicker");
        text.Should().Contain("md:grid-cols-2");
        text.Should().Contain("md:grid-cols-3");

        text.Should().NotContain("TValue=\"string\" @bind-Value=\"_movementForm.Quantity\"");
        text.Should().NotContain("TValue=\"string\" @bind-Value=\"_receiveForm.Quantity\"");
        text.Should().NotContain("ParseDate(");
    }

    [Test]
    public void ProductDetailNavigation_UsesShellSidebarAndHumanReadableBreadcrumbs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var controlPanelRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components");
        var productDetailPath = Path.Combine(
            controlPanelRoot,
            "Pages",
            "Inventario",
            "ProductDetail.razor");
        var productSidebarPath = Path.Combine(
            controlPanelRoot,
            "Layout",
            "ProductDetailSidebar.razor");
        var mainLayoutPath = Path.Combine(
            controlPanelRoot,
            "Layout",
            "MainLayout.razor");

        var productDetailText = File.ReadAllText(productDetailPath);
        var productSidebarText = File.ReadAllText(productSidebarPath);
        var mainLayoutText = File.ReadAllText(mainLayoutPath);

        productDetailText.Should().Contain("@page \"/inventario/products/{Id:guid}\"");
        productDetailText.Should().Contain("@page \"/inventario/products/{Id:guid}/{Section}\"");
        productDetailText.Should().NotContain("ProductSectionNav");
        productDetailText.Should().NotContain("ProductSectionHref");
        productDetailText.Should().NotContain("ProductSectionNavClass");
        productDetailText.Should().NotContain("xf-detail-sidebar");
        productDetailText.Should().NotContain("Back to Products");
        productDetailText.Should().NotContain("Back to products");
        productDetailText.Should().NotContain("AriaLabel=\"Back to products\"");

        productSidebarText.Should().Contain("Label=\"Product List\"");
        productSidebarText.Should().Contain("Href=\"/inventario/products\"");
        productSidebarText.Should().Contain("Product Detail");
        productSidebarText.Should().Contain("Label=\"Summary\"");
        productSidebarText.Should().Contain("Label=\"Stock\"");
        productSidebarText.Should().Contain("Label=\"Lots / Batches\"");
        productSidebarText.Should().Contain("Label=\"Replenishment\"");
        productSidebarText.Should().Contain("Label=\"Variations\"");
        productSidebarText.Should().Contain("Label=\"Transactions\"");

        mainLayoutText.Should().Contain("BuildInventarioProductBreadcrumbs");
        mainLayoutText.Should().Contain("RefreshBreadcrumbEntityLabels");
        mainLayoutText.Should().Contain("GetProductBreadcrumbLabel");
        mainLayoutText.Should().Contain("DataContext.Query<Product>()");
        mainLayoutText.Should().Contain("ProductSectionLabel(normalizedSection)");
        mainLayoutText.Should().Contain("ShortId(productId)");

        var getBreadcrumbsBody = Regex.Match(
            mainLayoutText,
            @"private\s+List<string>\s+GetBreadcrumbs\(\)\s*\{(?<body>.*?)\n\s*\}",
            RegexOptions.Singleline).Groups["body"].Value;

        getBreadcrumbsBody.Should().Contain("TryGetInventarioProductDetailRoute");
        getBreadcrumbsBody.Should().Contain("BuildInventarioProductBreadcrumbs");
        getBreadcrumbsBody.Should().NotContain("TitleCase(s.Replace");
    }

    [Test]
    public void InventarioPages_Toasts_DoNotExposeRawTechnicalDetails()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Inventario");

        var forbiddenPatterns = new[]
        {
            @"ToastService\.[A-Za-z]+\s*\([^;]*(?:ex|exception)\.Message",
            @"ToastService\.[A-Za-z]+\s*\([^;]*result\.Message",
            @"ToastService\.[A-Za-z]+\s*\([^;]*SqlException",
            @"ToastService\.[A-Za-z]+\s*\([^;]*DbUpdateException",
            @"ToastService\.[A-Za-z]+\s*\([^;]*stack trace"
        };

        var offenders = Directory.EnumerateFiles(pagesRoot, "*.razor", SearchOption.AllDirectories)
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                return forbiddenPatterns
                    .Where(pattern => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
                    .Select(pattern => $"{Path.GetRelativePath(repositoryRoot.FullName, path)} matched {pattern}");
            })
            .ToArray();

        offenders.Should().BeEmpty("Inventario toasts should use semantic user-facing copy while logging technical details separately");
    }

    [Test]
    public void InventarioListPages_TabularSurfaces_UseFilteredBlazorBlueprintDataGrids()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Inventario");

        foreach (var page in InventarioListPagesRequiringFilteredDataGrids)
        {
            var pagePath = Path.Combine(pagesRoot, page);
            var text = File.ReadAllText(pagePath);

            text.Should().NotContain("<table", $"{page} should use BlazorBlueprint data grids instead of raw tables");
            text.Should().Contain("<BbDataGrid", $"{page} should use BbDataGrid for list/report tabular records");
            text.Should().NotMatchRegex(
                @"<BbDataGridTemplateColumn\b[^>]*Title=""Actions""[^>]*Filterable=""true""",
                $"{page} should not expose filters on command/action columns");

            var grids = Regex.Matches(text, @"<BbDataGrid\b[\s\S]*?</BbDataGrid>", RegexOptions.Multiline);
            grids.Should().NotBeEmpty($"{page} should render at least one data grid");

            foreach (Match grid in grids)
            {
                grid.Value.Should().Contain(
                    "Filterable=\"true\"",
                    $"{page} data grids should expose native column filtering on useful business columns");
            }
        }
    }

    [Test]
    public void InventarioDetailPages_TabularSurfaces_UseFilteredBlazorBlueprintDataGrids()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Inventario");

        foreach (var page in InventarioDetailPagesRequiringFilteredDataGrids)
        {
            var pagePath = Path.Combine(pagesRoot, page);
            var text = File.ReadAllText(pagePath);

            text.Should().NotContain("<table", $"{page} should use BlazorBlueprint data grids instead of raw tables");
            text.Should().Contain("<BbDataGrid", $"{page} should use BbDataGrid for detail tabular records");
            text.Should().NotMatchRegex(
                @"<BbDataGridTemplateColumn\b[^>]*Title=""Actions""[^>]*Filterable=""true""",
                $"{page} should not expose filters on command/action columns");

            var grids = Regex.Matches(text, @"<BbDataGrid\b[\s\S]*?</BbDataGrid>", RegexOptions.Multiline);
            grids.Should().NotBeEmpty($"{page} should render at least one data grid");

            foreach (Match grid in grids)
            {
                grid.Value.Should().Contain(
                    "Filterable=\"true\"",
                    $"{page} detail grids should expose native column filtering on useful business columns");
            }
        }
    }

    [Test]
    public void InventarioWorkflowPages_DomainEntitySelectors_UseSharedEntityPicker()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "Components",
            "Pages",
            "Inventario");

        foreach (var page in InventarioWorkflowPagesRequiringEntityPickers)
        {
            var pagePath = Path.Combine(pagesRoot, page);
            var text = File.ReadAllText(pagePath);

            text.Should().Contain("<XfEntityPicker", $"{page} should use the shared entity picker for domain entity selectors");
            text.Should().NotContain("<BbCombobox", $"{page} should not use plain comboboxes for product, warehouse, location, lot, supplier, or purchase-order selectors");
            text.Should().NotContain("<BbFormFieldCombobox", $"{page} should not use form-field comboboxes for domain entity selectors");
        }
    }

    [Test]
    public void AgentGuidance_PortalTables_PrefersFilteredBlazorBlueprintDataGrids()
    {
        var repositoryRoot = FindRepositoryRoot();
        var agentsPath = Path.Combine(repositoryRoot.FullName, "AGENTS.md");
        var blazorGuidePath = Path.Combine(
            repositoryRoot.FullName,
            "docs",
            "solutions",
            "tooling-decisions",
            "blazor-blueprint-portal-agent-guide.md");

        var agentsText = File.ReadAllText(agentsPath);
        var guideText = File.ReadAllText(blazorGuidePath);

        agentsText.Should().Contain("BbDataGrid");
        agentsText.Should().Contain("Filterable=\"true\"");
        agentsText.Should().Contain("FilterBy");
        agentsText.Should().Contain("Do not create raw HTML tables");

        guideText.Should().Contain("BbDataGrid");
        guideText.Should().Contain("Filterable=\"true\"");
        guideText.Should().Contain("FilterBy");
        guideText.Should().Contain("Prefer it over raw `<table>` markup");
        guideText.Should().Contain("Do not make action/command columns filterable");
    }

    private static IEnumerable<string> FindDirectMutations(
        DirectoryInfo repositoryRoot,
        string path,
        string text,
        string entity)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot.FullName, path);
        var operations = new[] { "Add", "Update", "Remove" };

        foreach (var operation in operations)
        {
            var inlinePattern = $@"DataContext\.{operation}\s*\(\s*new\s+{Regex.Escape(entity)}\b";
            if (Regex.IsMatch(text, inlinePattern, RegexOptions.Multiline))
                yield return $"{relativePath} directly {operation.ToLowerInvariant()}s {entity}";

            var genericPattern = $@"DataContext\.{operation}\s*<\s*{Regex.Escape(entity)}\s*>";
            if (Regex.IsMatch(text, genericPattern, RegexOptions.Multiline))
                yield return $"{relativePath} directly {operation.ToLowerInvariant()}s {entity}";
        }

        foreach (Match declaration in Regex.Matches(
            text,
            $@"(?:var|{Regex.Escape(entity)})\s+(\w+)\s*=\s*new\s+{Regex.Escape(entity)}\b",
            RegexOptions.Multiline))
        {
            var variableName = Regex.Escape(declaration.Groups[1].Value);
            foreach (var operation in operations)
            {
                var variableMutationPattern = $@"DataContext\.{operation}\s*\(\s*{variableName}\s*\)";
                if (Regex.IsMatch(text, variableMutationPattern, RegexOptions.Multiline))
                    yield return $"{relativePath} directly {operation.ToLowerInvariant()}s {entity}";
            }
        }
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
