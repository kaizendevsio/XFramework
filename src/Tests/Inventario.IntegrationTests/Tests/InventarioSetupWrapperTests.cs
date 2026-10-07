using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture, NonParallelizable]
[Category(TestCategories.Integration), Category(TestCategories.Inventario), Category(TestCategories.Wrappers)]
public sealed class InventarioSetupWrapperTests
{
    [Test]
    public async Task Setup_ReadConfirmReplayAndPreferences_UseGeneratedWrapperAndPersistDefaults()
    {
        var wrapper = InventarioIntegrationTestFixture.ServiceWrapper;
        var tenant = InventarioIntegrationTestFixture.TestTenantId;
        var metadata = new RequestMetadata { RequestedTenantId = tenant, RequestId = Guid.NewGuid() };
        var initial = await wrapper.GetInventarioSetup(new GetInventarioSetupRequest { Metadata = metadata });
        initial.IsSuccess.Should().BeTrue(initial.Message);
        initial.Response!.TenantId.Should().Be(tenant);
        initial.Response.CanManage.Should().BeTrue();
        var code = "QA-SETUP-" + Guid.NewGuid().ToString("N")[..8];
        var request = new CompleteInventarioSetupRequest
        {
            Metadata = metadata, CompletionRequestId = Guid.NewGuid(), ExpectedConcurrencyStamp = initial.Response.ConcurrencyStamp,
            Mode = InventarioSetupMode.Advanced, LowStockThreshold = 12, DefaultCurrency = "SGD",
            Warehouse = new() { Code = code, Name = "Synthetic setup wrapper warehouse", IsDefault = false },
            Location = new() { Code = code, Name = "Synthetic setup wrapper location", IsPickable = true }
        };
        var saved = await wrapper.CompleteInventarioSetup(request);
        saved.IsSuccess.Should().BeTrue(saved.Message);
        saved.Response!.CompletedAt.Should().NotBeNull();
        saved.Response.Mode.Should().Be(InventarioSetupMode.Advanced);
        var replay = await wrapper.CompleteInventarioSetup(request);
        replay.IsSuccess.Should().BeTrue(replay.Message);
        replay.Response!.WarehouseId.Should().Be(saved.Response.WarehouseId);
        replay.Response.LocationId.Should().Be(saved.Response.LocationId);
        var updated = await wrapper.UpdateInventarioPreferences(new UpdateInventarioPreferencesRequest
        {
            Metadata = metadata, ExpectedConcurrencyStamp = saved.Response.ConcurrencyStamp,
            LowStockThreshold = 18, DefaultCurrency = "USD"
        });
        updated.IsSuccess.Should().BeTrue(updated.Message);
        var reloaded = await wrapper.GetInventarioSetup(new GetInventarioSetupRequest { Metadata = metadata });
        reloaded.IsSuccess.Should().BeTrue(reloaded.Message);
        reloaded.Response!.LowStockThreshold.Should().Be(18);
        reloaded.Response.DefaultCurrency.Should().Be("USD");
        reloaded.Response.ShouldPrompt.Should().BeFalse();
    }
}
