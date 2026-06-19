using System.Net;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using XFramework.Core.Services.FeatureGates;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Inventario)]
public sealed class SmokeTests : InventarioTestBase
{
    [Test]
    [Category(TestCategories.Auth)]
    public async Task GetWarehouses_UnauthenticatedRequest_ReturnsUnauthorizedOrForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/inventario/warehouses");
        request.Headers.Add(TestAuthHeaders.Unauthenticated, "true");

        using var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Test]
    [Category(TestCategories.FeatureGates)]
    public async Task CreateWarehouse_WarehousingFeatureDisabled_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        await using (var db = CreateDbContext())
        {
            db.Set<Tenant>().Add(new Tenant
            {
                Id = tenantId,
                TenantId = tenantId,
                Name = "Inventario Disabled Tenant",
                Description = "Feature gate test tenant",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            await TestInventarioSeed.SetInventarioFeature(db, string.Empty, true, tenantId);
            await TestInventarioSeed.SetInventarioFeature(db, TenantModuleFeatureKeys.WarehousingSubFeature, false, tenantId);

            var seededFeature = await db.Set<TenantModuleFeature>()
                .IgnoreQueryFilters()
                .SingleAsync(x =>
                    x.TenantId == tenantId &&
                    x.ModuleKey == TenantModuleFeatureKeys.Inventario &&
                    x.SubFeatureKey == TenantModuleFeatureKeys.WarehousingSubFeature);
            seededFeature.IsEnabled.Should().BeFalse();
        }

        using (var scope = InventarioIntegrationTestFixture.Services.CreateScope())
        {
            var featureService = scope.ServiceProvider.GetRequiredService<ITenantModuleFeatureService>();
            var featureResult = await featureService.IsEnabledAsync(
                tenantId,
                TenantModuleFeatureKeys.Inventario,
                TenantModuleFeatureKeys.WarehousingSubFeature);
            featureResult.IsSuccess.Should().BeTrue();
            featureResult.Data.Should().BeFalse();
        }

        var metadata = CreateMetadata();
        metadata.TenantId = tenantId;

        var result = await InventarioIntegrationTestFixture.ServiceWrapper.CreateWarehouse(
            new CreateWarehouseRequest
            {
                Metadata = metadata,
                Code = UniqueCode("WH"),
                Name = "Disabled Warehouse"
            });

        result.IsSuccess.Should().BeFalse();
        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    [Category(TestCategories.DataContext)]
    public async Task RemoteQuery_ToListAsync_ReturnsProductsFromInventarioService()
    {
        await using var db = CreateDbContext();
        var product = await TestInventarioSeed.SeedProduct(db);
        var ctx = InventarioIntegrationTestFixture.DataContext;

        var products = await ctx.Query<Product>()
            .Where(x => x.Id == product.Id)
            .ToListAsync();

        products.Should().ContainSingle(x => x.Id == product.Id);
    }
}
