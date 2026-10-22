using System.Net;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture, NonParallelizable]
[Category("Kind:Integration"), Category("Module:Inventario"), Category("Area:Wrappers"), Category("Area:Brands")]
public sealed class ProductBrandWrapperTests : InventarioTestBase
{
    [Test]
    public async Task Brands_SearchPreviewConfirm_UsesGeneratedWrapperAndPersistsOnlySelectedCategory()
    {
        // The catalog gate checks the persisted credential and role as well as token capabilities.
        var token = TestInvocationIdentityExtensions.CreateTestActorToken(
            InventarioIntegrationTestFixture.TestTenantId,
            InventarioIntegrationTestFixture.TestCredentialId,
            InventarioIntegrationTestFixture.TestIdentityId, Guid.NewGuid(),
            [], [XFrameworkActorCapabilities.IdentityTenantsManage]);
        using var actor = TestInvocationActorTokenScope.Push(token);
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var source = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        var target = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        source.Brand = " Acme  tools ";
        target.Brand = "TARGET";
        await db.SaveChangesAsync();
        var wrapper = InventarioIntegrationTestFixture.ServiceWrapper;
        var search = await wrapper.SearchProductBrands(new SearchProductBrandsRequest { Metadata = CreateMetadata(), CategoryId = category.Id });
        search.IsSuccess.Should().BeTrue(search.Message);
        search.Response.Should().Equal("ACME TOOLS", "TARGET");
        var preview = await wrapper.PreviewProductBrandMerge(new PreviewProductBrandMergeRequest
        {
            Metadata = CreateMetadata(), CategoryId = category.Id, SourceBrand = "Acme tools", TargetBrand = "target"
        });
        preview.IsSuccess.Should().BeTrue(preview.Message);
        preview.Response!.AffectedProductCount.Should().Be(1);
        var request = new MergeProductBrandsRequest
        {
            Metadata = CreateMetadata(), CategoryId = category.Id, SourceBrand = preview.Response.SourceBrand,
            TargetBrand = preview.Response.TargetBrand, ExpectedProductCount = preview.Response.AffectedProductCount
        };
        var unconfirmed = await wrapper.MergeProductBrands(request);
        unconfirmed.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        var merged = await wrapper.MergeProductBrands(request with { Confirmed = true });
        merged.IsSuccess.Should().BeTrue(merged.Message);
        await using var read = CreateDbContext();
        var persisted = await read.Set<Product>().SingleAsync(x => x.Id == source.Id);
        persisted.Brand.Should().Be("TARGET");
        persisted.StockQuantity.Should().Be(source.StockQuantity);
        persisted.Price.Should().Be(source.Price);
        var replay = await wrapper.MergeProductBrands(request with { Confirmed = true });
        replay.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Brands_OrdinaryActorMaySearchButCannotMerge_AndSpoofedTenantIsDenied()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var wrapper = InventarioIntegrationTestFixture.ServiceWrapper;
        var forged = await wrapper.SearchProductBrands(new SearchProductBrandsRequest
        {
            Metadata = new() { RequestedTenantId = Guid.NewGuid() }, CategoryId = category.Id
        });
        forged.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);

        var token = TestInvocationIdentityExtensions.CreateTestActorToken(
            InventarioIntegrationTestFixture.TestTenantId,
            InventarioIntegrationTestFixture.TestCredentialId,
            InventarioIntegrationTestFixture.TestIdentityId, Guid.NewGuid(), [], []);
        using var actor = TestInvocationActorTokenScope.Push(token);
        var search = await wrapper.SearchProductBrands(new SearchProductBrandsRequest { Metadata = CreateMetadata(), CategoryId = category.Id });
        search.IsSuccess.Should().BeTrue(search.Message);
        var preview = await wrapper.PreviewProductBrandMerge(new PreviewProductBrandMergeRequest
        {
            Metadata = CreateMetadata(), CategoryId = category.Id, SourceBrand = "A", TargetBrand = "B"
        });
        preview.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        var merge = await wrapper.MergeProductBrands(new MergeProductBrandsRequest
        {
            Metadata = CreateMetadata(), CategoryId = category.Id, SourceBrand = "A", TargetBrand = "B",
            Confirmed = true, ExpectedProductCount = 1
        });
        merge.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

