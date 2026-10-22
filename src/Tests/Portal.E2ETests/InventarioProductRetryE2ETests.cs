using FluentAssertions;
using MemoryPack;
using Microsoft.Playwright;
using Moq;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;

namespace Portal.E2ETests;

public sealed partial class InventarioSetupWizardE2ETests
{
    [TestCase(false)]
    [TestCase(true)]
    [Category("Area:ProductWorkflow")]
    public async Task ProductWorkflow_StockRetry_CloseAndReopenPreservesReceiptUntilConfirmed(bool committed)
    {
        PrepareProductWorkflow(hasVariant: true);
        _state.FailNextBasicStock = true;
        _state.ThrowNextBasicStock = true;
        _state.CommitStockBeforeFailure = committed;
        await OpenWorkflowProduct();
        var dialog = await OpenBasicStock(variant: true);
        await dialog.GetByLabel("Quantity to add", new() { Exact = true }).FillAsync("4.25");
        await dialog.GetByLabel("Reference or reason", new() { Exact = true }).FillAsync("Uncertain receipt");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add stock", Exact = true }).ClickAsync();
        await Expect(dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync("The result could not be confirmed");
        var original = _state.BasicStockRequests.Should().ContainSingle().Subject;
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);

        dialog = await OpenBasicStock(variant: false);
        await Expect(dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync("do not enter it again as a new receipt");
        await Expect(dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync(original.IdempotencyKey);
        await Expect(dialog.GetByLabel("Quantity to add", new() { Exact = true })).ToHaveValueAsync("4.25");
        await Expect(dialog.GetByLabel("Quantity to add", new() { Exact = true })).ToBeDisabledAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Variant", Exact = true })).ToContainTextAsync("Large");
        await Screenshot($"product-stock-reopen-unresolved-{committed}-1920");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add stock", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(WorkflowAvailableStock).ToHaveTextAsync("4.25");
        _state.BasicStockRequests.ToArray()[1].Should().BeEquivalentTo(original);
        _state.WorkflowBalances.Single().OnHandQuantity.Should().Be(4.25m);

        dialog = await OpenBasicStock(variant: false);
        await Expect(dialog.GetByLabel("Quantity to add", new() { Exact = true })).ToBeEnabledAsync();
        await Expect(dialog.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add stock", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        _state.BasicStockRequests.Last().IdempotencyKey.Should().NotBe(original.IdempotencyKey);
        AssertNoDirectStockWrites();
        _errors.Should().BeEmpty();
    }

    [TestCase("rejected")]
    [TestCase("before-commit")]
    [TestCase("after-commit")]
    [Category("Area:ProductWorkflow")]
    public async Task ProductWorkflow_CategoryRetry_IsolatedBatchAndOriginalIdSurviveReopening(string failure)
    {
        PrepareProductWorkflow();
        _state.NextCategoryFailure = failure;
        await Page.GotoAsync(_app.Urls.Single() + "/inventario-products-fixture");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create Product", Exact = true }).ClickAsync();
        var productDialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Create Product", Exact = true });
        await productDialog.GetByLabel("Name", new() { Exact = true }).FillAsync("Retained product draft");
        var categoryDialog = await OpenRetryCategory(productDialog);
        await categoryDialog.GetByLabel("Name", new() { Exact = true }).FillAsync("Original category");
        await Expect(categoryDialog.GetByRole(AriaRole.Button, new() { Name = "Create category", Exact = true })).ToBeEnabledAsync();
        await categoryDialog.GetByRole(AriaRole.Button, new() { Name = "Create category", Exact = true }).ClickAsync();
        await Expect(categoryDialog.GetByRole(AriaRole.Alert)).ToContainTextAsync(failure == "rejected" ? "Could not create category" : "unconfirmed");
        var original = _state.CategoryCreates.Should().ContainSingle().Subject;
        await categoryDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(categoryDialog).ToHaveCountAsync(0);
        categoryDialog = await OpenRetryCategory(productDialog);
        await Expect(categoryDialog.GetByLabel("Name", new() { Exact = true })).ToHaveValueAsync("Original category");
        if (failure == "rejected")
            await categoryDialog.GetByLabel("Name", new() { Exact = true }).FillAsync("Corrected category");
        else
            await Expect(categoryDialog.GetByLabel("Name", new() { Exact = true })).ToBeDisabledAsync();
        await Screenshot($"product-category-retry-{failure}-1920");
        await categoryDialog.GetByRole(AriaRole.Button, new() { Name = "Create category", Exact = true }).ClickAsync();
        await Expect(categoryDialog).ToHaveCountAsync(0);
        await Expect(productDialog.GetByLabel("Name", new() { Exact = true })).ToHaveValueAsync("Retained product draft");
        await Expect(productDialog.GetByRole(AriaRole.Button, new() { Name = "Category", Exact = true }))
            .ToContainTextAsync(failure == "rejected" ? "Corrected category" : "Original category");
        _state.CategoryCreates.Should().HaveCount(failure == "after-commit" ? 1 : 2)
            .And.OnlyContain(category => category.Id == original.Id && category.TenantId == _state.FirstTenant);
        _state.WorkflowCategories.Should().ContainSingle().Which.Id.Should().Be(original.Id);
        _state.CategorySaveBatches.Should().OnlyContain(batch => batch.Changes.Count == 1 && batch.Metadata!.RequestedTenantId == _state.FirstTenant);
        if (failure != "rejected") _state.CategoryQueries.Should().ContainSingle();

        categoryDialog = await OpenRetryCategory(productDialog);
        await Expect(categoryDialog.GetByLabel("Name", new() { Exact = true })).ToBeEnabledAsync();
        await categoryDialog.GetByLabel("Name", new() { Exact = true }).FillAsync("Next category");
        await Expect(categoryDialog.GetByRole(AriaRole.Button, new() { Name = "Create category", Exact = true })).ToBeEnabledAsync();
        await categoryDialog.GetByRole(AriaRole.Button, new() { Name = "Create category", Exact = true }).ClickAsync();
        await Expect(categoryDialog).ToHaveCountAsync(0);
        _state.CategoryCreates.Last().Id.Should().NotBe(original.Id);
        _state.WorkflowCategories.Should().HaveCount(2);
        _state.CategorySaveBatches.Last().Changes.Should().ContainSingle();
        _data.Verify(x => x.Add(It.IsAny<ProductCategory>()), Times.Never());
        AssertNoDirectStockWrites();
        _errors.Should().BeEmpty();
    }

    private async Task<ILocator> OpenRetryCategory(ILocator productDialog)
    {
        await productDialog.GetByRole(AriaRole.Button, new() { Name = "Category", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Create New", Exact = true }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Create Category", Exact = true });
        await Expect(dialog).ToBeVisibleAsync();
        return dialog;
    }
}

public sealed partial class SetupBrowserState
{
    public string? NextCategoryFailure { get; set; }
    public bool CommitStockBeforeFailure { get; set; }
    public List<SaveChangesRequest> CategorySaveBatches { get; } = [];
    public List<QueryDescriptor> CategoryQueries { get; } = [];
    public Dictionary<string, QueryResponse<StockPostingResponse>> PostedWorkflowStock { get; } = [];

    private void ResetProductRetries()
    {
        NextCategoryFailure = null;
        CommitStockBeforeFailure = false;
        CategorySaveBatches.Clear(); CategoryQueries.Clear(); PostedWorkflowStock.Clear();
    }

    public byte[] SaveWorkflowCategory(byte[] bytes)
    {
        var batch = MemoryPackSerializer.Deserialize<SaveChangesRequest>(bytes)!;
        CategorySaveBatches.Add(batch);
        var change = batch.Changes.Should().ContainSingle().Subject;
        change.EntityTypeName.Should().Be(nameof(ProductCategory));
        change.Operation.Should().Be(ChangeOperation.Add);
        var category = MemoryPackSerializer.Deserialize<ProductCategory>(change.SerializedEntity)!;
        batch.Metadata!.RequestedTenantId.Should().Be(category.TenantId);
        CategoryCreates.Enqueue(category);
        var failure = NextCategoryFailure;
        NextCategoryFailure = null;
        if (failure == "rejected") return MemoryPackSerializer.Serialize(DataContextResult.Failure("Synthetic validation failure", 400));
        if (failure == "before-commit") throw new TimeoutException("Synthetic failure before commit");
        WorkflowCategories.All(x => x.Id != category.Id).Should().BeTrue("uncertain creates must reconcile before trying another insert");
        WorkflowCategories.Add(category);
        if (failure == "after-commit") throw new TimeoutException("Synthetic failure after commit");
        return MemoryPackSerializer.Serialize(DataContextResult.Success());
    }

    public byte[] FindWorkflowCategory(byte[] bytes)
    {
        var query = MemoryPackSerializer.Deserialize<QueryDescriptor>(bytes)!;
        CategoryQueries.Add(query);
        query.EntityTypeName.Should().Be(nameof(ProductCategory));
        query.NoCache.Should().BeTrue();
        var id = (Guid)query.Filters.Single(x => x.PropertyName == nameof(ProductCategory.Id)).Value!;
        var tenant = (Guid)query.Filters.Single(x => x.PropertyName == nameof(ProductCategory.TenantId)).Value!;
        query.Metadata!.RequestedTenantId.Should().Be(tenant);
        return MemoryPackSerializer.Serialize(WorkflowCategories.SingleOrDefault(x => x.Id == id && x.TenantId == tenant));
    }
}
