using XFramework.TestInfrastructure;
using System.Text.RegularExpressions;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.Inventario)]
[Category(TestCategories.ControlPanelContract)]
public sealed class ControlPanelContractTests
{
    private static readonly string[] BusinessWorkflowEntities =
    [
        "InventoryReorderRule",
        "Warehouse",
        "InventoryLocation",
        "InventoryLot",
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

    [Test]
    public void InventarioPages_BusinessWorkflowMutations_DoNotUseDirectRemoteDataContextMutation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "ControlPanel.Server",
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
            "ControlPanel.Server",
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
