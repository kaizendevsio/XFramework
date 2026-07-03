using XFramework.TestInfrastructure;

namespace POS.Api.Tests;

[TestFixture]
[Category(TestCategories.POS)]
public sealed class PosOrchestrationContractTests
{
    [Test]
    public void CheckoutService_UsesInventarioReservationsWalletPostingAndRecoveryStates()
    {
        var source = ReadSource("src", "Modules", "XFramework.POS", "POS.Api", "Services", "PosSalesService.cs");

        source.Should().Contain("GetSellableProduct", "checkout must snapshot current Inventario catalog prices");
        source.Should().Contain("Result<PosSaleReceiptResponse>.Conflict", "expected unit price conflicts should fail as conflicts");
        source.Should().Contain("ReserveInventory", "checkout must reserve inventory before payment capture");
        source.Should().Contain("ReferenceType = PosServiceHelpers.SaleLineReferenceType");
        source.Should().Contain("GetReservations", "reservation IDs must be persisted for recovery");
        source.Should().Contain("IncrementWallet", "cash drawer payment must post through Wallets");
        source.Should().Contain("TransferWallet", "wallet tender must post through Wallets");
        source.Should().Contain("ReleaseReservationsAsync", "payment failure must release inventory reservations");
        source.Should().Contain("FulfillReservation", "paid sales must fulfill Inventario reservations");
        source.Should().Contain("InventoryFulfillmentFailed", "paid-but-unfulfilled sales need a recoverable status");
        source.Should().Contain("RetryFulfillmentAsync", "fulfillment retry must be an explicit POS command");
        source.Should().Contain("IdempotencyKey", "cross-module calls must use stable idempotency keys");
        source.Should().Contain("IPosRequestContextResolver", "checkout must use trusted tenant/cashier context");
        source.Should().Contain("BuildSaleRequestHash", "checkout replays must compare the original request payload");
        source.Should().Contain("ContinueCheckoutAsync", "checkout replays must resume the persisted sale state");
    }

    [Test]
    public void ReturnsService_UsesInventarioReturnMovementAndWalletRefunds()
    {
        var source = ReadSource("src", "Modules", "XFramework.POS", "POS.Api", "Services", "PosReturnsService.cs");

        source.Should().Contain("PostStockMovement", "returns must post inventory through Inventario");
        source.Should().Contain("InventoryMovementType.Return");
        source.Should().Contain("ReferenceType = PosServiceHelpers.ReturnLineReferenceType");
        source.Should().Contain("DecrementWallet", "cash refunds must debit the cash drawer wallet");
        source.Should().Contain("TransferWallet", "wallet refunds must reverse transfer through Wallets");
        source.Should().Contain("TransactionPurpose.Refund");
        source.Should().Contain("RetryAsync", "recoverable return failures need an explicit retry command");
        source.Should().Contain("InventoryPostFailed", "inventory-post failures need a retryable state");
        source.Should().Contain("RefundFailed", "refund failures need a retryable state");
        source.Should().Contain("BuildReturnRequestHash", "return replays must compare the original request payload");
        source.Should().NotContain("db.Set<Wallet", "POS must not directly mutate Wallets tables");
        source.Should().NotContain("db.Set<InventoryMovement", "POS must not directly mutate Inventario tables");
    }

    [Test]
    public void CartService_SuspendsWithoutReservationsAndConvertsThroughCheckout()
    {
        var source = ReadSource("src", "Modules", "XFramework.POS", "POS.Api", "Services", "PosCartService.cs");

        source.Should().Contain("PosCartStatus.Suspended", "cashiers must be able to suspend draft carts");
        source.Should().Contain("SearchPosCartsRequest", "multiple suspended carts need a wrapper-searchable list");
        source.Should().Contain("BuildCatalogWarningsAsync", "resumed carts should surface stale catalog information");
        source.Should().Contain("ValidateCurrentPricesAsync", "checkout from a draft must revalidate catalog price");
        source.Should().Contain("BuildCheckoutRequest", "cart checkout should reuse the existing POS sale checkout flow");
        source.Should().Contain("salesService.CheckoutAsync", "cart conversion must delegate to sale orchestration");
        source.Should().Contain("POS.CartCheckout", "cart checkout needs a stable idempotency key");
        source.Should().NotContain("ReserveInventory", "suspending a draft cart must not reserve inventory");
        source.Should().NotContain("IncrementWallet", "draft carts must not post Wallets ledger entries");
        source.Should().NotContain("TransferWallet", "draft carts must not post Wallets ledger entries");
    }

    [Test]
    public void Program_GatesPosRoutesBehindTenantFeature()
    {
        var source = ReadSource("src", "Modules", "XFramework.POS", "POS.Api", "Program.cs");

        source.Should().Contain("TenantModuleFeatureKeys.PosCarts");
        source.Should().Contain("TenantModuleFeatureKeys.PosRegisters");
        source.Should().Contain("TenantModuleFeatureKeys.PosSales");
        source.Should().Contain("TenantModuleFeatureKeys.PosReturns");
        source.Should().Contain("RequireFeature(TenantModuleFeatureKeys.Pos, \"/api/pos\")");
        source.Should().Contain("MapGeneratedEndpoints");
    }

    private static string ReadSource(params string[] segments)
    {
        var repositoryRoot = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([repositoryRoot.FullName, .. segments]));
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
