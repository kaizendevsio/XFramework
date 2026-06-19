using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Inventario)]
[Category(TestCategories.Catalog)]
public sealed class CatalogTests : InventarioTestBase
{
    [Test]
    public async Task SaveChangesAsync_RemoteProductCategoryCreate_PersistsThroughInventarioDataContext()
    {
        var ctx = InventarioIntegrationTestFixture.DataContext;
        var category = new ProductCategory
        {
            Id = Guid.NewGuid(),
            TenantId = InventarioIntegrationTestFixture.TestTenantId,
            Name = $"Category {Guid.NewGuid():N}",
            Description = "Remote DataContext category",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        ctx.Add(category);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        await using var db = CreateDbContext();
        var persisted = await db.Set<ProductCategory>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == category.Id);
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(InventarioIntegrationTestFixture.TestTenantId);
    }

    [Test]
    public async Task SaveChangesAsync_RemoteProductCreate_PersistsWithCategory()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var ctx = InventarioIntegrationTestFixture.DataContext;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = InventarioIntegrationTestFixture.TestTenantId,
            CategoryId = category.Id,
            Name = $"Product {Guid.NewGuid():N}",
            SKU = $"SKU-{Guid.NewGuid():N}"[..18],
            Price = 25m,
            IsAvailable = true,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        ctx.Add(product);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        var persisted = await db.Set<Product>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == product.Id);
        persisted.Should().NotBeNull();
        persisted!.CategoryId.Should().Be(category.Id);
    }
}
