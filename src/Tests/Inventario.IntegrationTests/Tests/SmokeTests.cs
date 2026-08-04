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
        metadata.RequestedTenantId = tenantId;

        var actorToken = TestInvocationIdentityExtensions.CreateTestActorToken(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            roles: ["SuperAdmin"],
            capabilities: []);
        using var actorScope = TestInvocationActorTokenScope.Push(actorToken);

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
    [Category(TestCategories.Auth)]
    public async Task CreateWarehouse_WithCallerSuppliedDifferentTenant_IsRejectedWithoutWriting()
    {
        var requestedTenantId = Guid.NewGuid();
        var code = UniqueCode("SPOOF");
        var metadata = CreateMetadata();
        metadata.RequestedTenantId = requestedTenantId;

        var result = await InventarioIntegrationTestFixture.ServiceWrapper.CreateWarehouse(
            new CreateWarehouseRequest
            {
                Metadata = metadata,
                Code = code,
                Name = "Rejected cross-tenant warehouse"
            });

        result.IsSuccess.Should().BeFalse();
        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using var db = CreateDbContext();
        var wasWritten = await db.Set<Warehouse>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Code == code);
        wasWritten.Should().BeFalse();
    }

    [Test]
    [Category(TestCategories.Auth)]
    public async Task CreateWarehouse_RestBodyRequestedTenantSpoof_IsRejectedWithoutWriting()
    {
        var requestedTenantId = Guid.NewGuid();
        var code = UniqueCode("REST-SPOOF");
        using var response = await HttpClient.PostAsJsonAsync(
            "/api/inventario/warehouses",
            new CreateWarehouseRequest
            {
                Metadata = new XFramework.Domain.Shared.BusinessObjects.RequestMetadata
                {
                    RequestedTenantId = requestedTenantId,
                    IpAddress = "203.0.113.55",
                    UserAgent = "spoofed-agent"
                },
                Code = code,
                Name = "Rejected REST cross-tenant warehouse"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using var db = CreateDbContext();
        var wasWritten = await db.Set<Warehouse>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Code == code);
        wasWritten.Should().BeFalse();
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

    [Test]
    [Category(TestCategories.Auth)]
    [Category(TestCategories.DataContext)]
    public async Task RemoteQuery_IgnoreQueryFiltersWithoutActorCapability_IsRejected()
    {
        var actorToken = TestInvocationIdentityExtensions.CreateTestActorToken(
            InventarioIntegrationTestFixture.TestTenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            roles: [],
            capabilities: []);

        using var actorScope = TestInvocationActorTokenScope.Push(actorToken);
        var ctx = InventarioIntegrationTestFixture.DataContext;

        var query = async () => await ctx.Query<Product>()
            .IgnoreQueryFilters()
            .ToListAsync();

        await query.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*failed*");
    }
}
