using System.Net;
using System.Reflection;
using FluentAssertions;
using Inventario.Integration.Drivers;
using MemoryPack;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Portal.Features.Inventario.Components;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Area:ProductWorkflow")]
public sealed class InventarioEntryRetryTests
{
    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.UnprocessableEntity)]
    public async Task StockEntry_ValidationRejection_UnlocksDraftAndSubmitsCorrectedPayload(HttpStatusCode rejectedStatus)
    {
        var tenantId = Guid.NewGuid();
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(tenantId);
        var wrapper = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        var attempts = new List<PostBasicProductStockRequest>();
        wrapper.Setup(x => x.PostBasicProductStock(It.IsAny<PostBasicProductStockRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostBasicProductStockRequest request, CancellationToken _) =>
            {
                attempts.Add(request);
                return new QueryResponse<StockPostingResponse>
                {
                    HttpStatusCode = attempts.Count == 1 ? rejectedStatus : HttpStatusCode.ServiceUnavailable
                };
            });
        var entry = new BasicProductStockEntry { Product = new() { Id = Guid.NewGuid(), TenantId = tenantId } };
        Property(entry, "TenantContext", tenant.Object);
        Property(entry, "Inventario", wrapper.Object);
        Invoke(entry, "Open");
        SetField(entry, "_quantity", 1.12345m);
        SetField(entry, "_reason", "Rejected draft");
        await (Task)Invoke(entry, "Save")!;

        Field<PostBasicProductStockRequest?>(entry, "_pendingRequest").Should().BeNull("400 and 422 must unlock the draft fields");
        Field<bool>(entry, "_open").Should().BeTrue();
        Field<bool>(entry, "_saving").Should().BeFalse();
        Field<string>(entry, "_error").Should().Contain("Stock could not be added");
        var variantId = Guid.NewGuid();
        SetField(entry, "_quantity", 2.5m);
        SetField(entry, "_reason", "  Corrected draft  ");
        SetField(entry, "_variantId", variantId.ToString());
        await (Task)Invoke(entry, "Save")!;

        attempts.Should().HaveCount(2);
        attempts[1].Should().NotBeSameAs(attempts[0]);
        attempts[1].Quantity.Should().Be(2.5m);
        attempts[1].Reason.Should().Be("Corrected draft");
        attempts[1].ProductVariationId.Should().Be(variantId);
        attempts[1].ProductId.Should().Be(entry.Product.Id);
        attempts[1].Metadata.RequestedTenantId.Should().Be(tenantId);
        attempts[0].Quantity.Should().Be(1.12345m);
        attempts[0].Reason.Should().Be("Rejected draft");
        Field<PostBasicProductStockRequest>(entry, "_pendingRequest").Should().BeSameAs(attempts[1],
            "an uncertain response to the corrected request must retain its payload for retry");
    }

    [Test]
    public void StockEntry_CancelBeforeSubmit_ReopeningStartsFreshReceipt()
    {
        var tenantId = Guid.NewGuid();
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(tenantId);
        var entry = new BasicProductStockEntry { Product = new() { Id = Guid.NewGuid(), TenantId = tenantId } };
        Property(entry, "TenantContext", tenant.Object);
        Invoke(entry, "Open");
        var originalKey = Field<string>(entry, "_key");
        SetField(entry, "_open", false);
        Invoke(entry, "Open");
        Field<string>(entry, "_key").Should().NotBe(originalKey);
        Field<PostBasicProductStockRequest?>(entry, "_pendingRequest").Should().BeNull();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StockEntry_InFlightRequest_CannotBeResetAndLateTenantResponseIsIgnored(bool success)
    {
        var tenantId = Guid.NewGuid();
        var selectedTenant = tenantId;
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(() => selectedTenant);
        var wrapper = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        var gate = new TaskCompletionSource<QueryResponse<StockPostingResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        PostBasicProductStockRequest? submitted = null;
        wrapper.Setup(x => x.PostBasicProductStock(It.IsAny<PostBasicProductStockRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostBasicProductStockRequest, CancellationToken>((request, _) => submitted = request).Returns(gate.Task);
        var refreshed = 0;
        var entry = new BasicProductStockEntry
        {
            Product = new() { Id = Guid.NewGuid(), TenantId = tenantId },
            Saved = EventCallback.Factory.Create(this, () => refreshed++)
        };
        Property(entry, "TenantContext", tenant.Object);
        Property(entry, "Inventario", wrapper.Object);
        Invoke(entry, "Open");
        var pending = (Task)Invoke(entry, "Save")!;
        submitted.Should().NotBeNull();
        SetField(entry, "_open", false);
        Invoke(entry, "Open");
        Field<bool>(entry, "_open").Should().BeFalse("an in-flight save must not be reopened as a new receipt");
        Field<PostBasicProductStockRequest>(entry, "_pendingRequest").Should().BeSameAs(submitted);
        selectedTenant = Guid.NewGuid();
        Invoke(entry, "OnParametersSet");
        gate.SetResult(new() { HttpStatusCode = success ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable });
        await pending;
        refreshed.Should().Be(0);
        Field<string?>(entry, "_error").Should().BeNull();
        submitted!.Metadata.RequestedTenantId.Should().Be(tenantId);
        wrapper.Verify(x => x.PostBasicProductStock(It.IsAny<PostBasicProductStockRequest>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task CategoryEntry_TenantSwitchDuringSave_DoesNotSelectOldTenantCategory()
    {
        var selectedTenant = Guid.NewGuid();
        var originalTenant = selectedTenant;
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(() => selectedTenant);
        var wrapper = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        var gate = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        SaveChangesRequest? submitted = null;
        wrapper.As<IDataContextServiceWrapper>().Setup(x => x.ExecuteChangesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<byte[], CancellationToken>((bytes, _) => submitted = MemoryPackSerializer.Deserialize<SaveChangesRequest>(bytes))
            .Returns(gate.Task);
        using var services = new ServiceCollection().AddSingleton(wrapper.Object).BuildServiceProvider();
        var callbacks = 0;
        var picker = new ProductCategoryPicker
        {
            Created = EventCallback.Factory.Create<ProductCategory>(this, _ => callbacks++),
            ValueChanged = EventCallback.Factory.Create<string?>(this, _ => callbacks++)
        };
        Property(picker, "Services", services);
        Property(picker, "TenantContext", tenant.Object);
        Invoke(picker, "OnParametersSet");
        Invoke(picker, "OpenCreate");
        SetField(picker, "_name", "Original tenant category");
        var pending = (Task)Invoke(picker, "Create")!;
        submitted.Should().NotBeNull();
        selectedTenant = Guid.NewGuid();
        Invoke(picker, "OnParametersSet");
        gate.SetResult(MemoryPackSerializer.Serialize(DataContextResult.Success()));
        await pending;
        callbacks.Should().Be(0);
        Field<bool>(picker, "_open").Should().BeFalse();
        Field<ProductCategory?>(picker, "_pendingCategory").Should().BeNull();
        submitted!.Metadata!.RequestedTenantId.Should().Be(originalTenant);
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object? Invoke(object target, string name) => target.GetType().GetMethod(name, Private)!.Invoke(target, null);
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private)!.SetValue(target, value);
    private static void Property(object target, string name, object value) => target.GetType().GetProperty(name, Private)!.SetValue(target, value);
}
