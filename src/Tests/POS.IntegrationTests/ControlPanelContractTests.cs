using System.Text.RegularExpressions;
using XFramework.TestInfrastructure;

namespace POS.IntegrationTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.POS)]
[Category(TestCategories.ControlPanelContract)]
public sealed class ControlPanelContractTests
{
    private static readonly string[] PosPages =
    [
        "Cashier.razor",
        "Registers.razor",
        "Sales.razor",
        "Returns.razor"
    ];

    [Test]
    public void PosPages_TabularSurfaces_UseFilteredBlazorBlueprintDataGrids()
    {
        var pagesRoot = GetPosPagesRoot();

        foreach (var page in PosPages)
        {
            var text = File.ReadAllText(Path.Combine(pagesRoot, page));

            text.Should().NotContain("<table", $"{page} should use BlazorBlueprint data grids instead of raw tables");
            text.Should().Contain("<BbDataGrid", $"{page} should use BbDataGrid for list/report tabular records");
            text.Should().Contain("Filterable=\"true\"", $"{page} should expose native filtering on useful columns");
            text.Should().NotMatchRegex(
                @"<BbDataGridTemplateColumn\b[^>]*Title=""Actions""[^>]*Filterable=""true""",
                $"{page} should not expose filters on command/action columns");
        }
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
        registers.Should().Contain("POS.CreatePosRegister(");
        sales.Should().Contain("POS.CancelPosSale(");
        sales.Should().Contain("POS.RetryPosSaleFulfillment(");
        returns.Should().Contain("POS.CreatePosReturn(");

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
    public void PosPages_HideWhenTenantFeatureIsUnavailable()
    {
        var pagesRoot = GetPosPagesRoot();

        foreach (var page in PosPages)
        {
            var text = File.ReadAllText(Path.Combine(pagesRoot, page));

            text.Should().Contain("_moduleEnabled");
            text.Should().Contain("ModuleUnavailable");
            text.Should().Contain("TenantModuleFeatureKeys.Pos");
        }
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
            "ControlPanel.Server",
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
