using XFramework.TestInfrastructure;

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
    public void InventarioPages_BusinessWorkflowCreates_DoNotUseDirectRemoteDataContextMutation()
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
                    .Where(entity => text.Contains($"DataContext.Add(new {entity}", StringComparison.Ordinal))
                    .Select(entity => $"{Path.GetRelativePath(repositoryRoot.FullName, path)} directly adds {entity}");
            })
            .ToArray();

        offenders.Should().BeEmpty("business workflow entities must go through IInventarioServiceWrapper endpoints");
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
