using Microsoft.EntityFrameworkCore;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Inventario)]
[Category(TestCategories.Catalog)]
[Category(TestCategories.DataContext)]
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
    [Category(TestCategories.ExtendedIntegration)]
    public async Task SaveChangesAsync_RemoteProductCategoryUpdate_PersistsThroughInventarioDataContext()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var ctx = InventarioIntegrationTestFixture.DataContext;

        category.Description = $"Updated {Guid.NewGuid():N}";
        ctx.Update(category);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        var persisted = await db.Set<ProductCategory>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == category.Id);
        persisted.Description.Should().Be(category.Description);
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

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    public async Task SaveChangesAsync_RemoteProductVariationCreate_PersistsWithProduct()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var product = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        var ctx = InventarioIntegrationTestFixture.DataContext;
        var variation = new ProductVariation
        {
            Id = Guid.NewGuid(),
            TenantId = InventarioIntegrationTestFixture.TestTenantId,
            ProductId = product.Id,
            Name = $"Variation {Guid.NewGuid():N}",
            AdditionalPrice = 2.5m,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        ctx.Add(variation);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeTrue(save.Message);
        var persisted = await db.Set<ProductVariation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == variation.Id);
        persisted.Should().NotBeNull();
        persisted!.ProductId.Should().Be(product.Id);
    }

    [Test]
    [Category(TestCategories.ExtendedIntegration)]
    [Category(TestCategories.Planning)]
    [Category(TestCategories.ControlPanelContract)]
    public async Task SaveChangesAsync_RemoteInventoryReorderRuleCreate_ReturnsNotRegisteredForRemoteMutation()
    {
        await using var db = CreateDbContext();
        var category = await TestInventarioSeed.SeedCategory(db);
        var product = await TestInventarioSeed.SeedProduct(db, categoryId: category.Id);
        var ctx = InventarioIntegrationTestFixture.DataContext;
        var rule = new InventoryReorderRule
        {
            Id = Guid.NewGuid(),
            TenantId = InventarioIntegrationTestFixture.TestTenantId,
            ProductId = product.Id,
            MinimumQuantity = 0,
            MaximumQuantity = 100,
            ReorderPoint = 10,
            ReorderQuantity = 25,
            IsActive = true,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        ctx.Add(rule);
        var save = await ctx.SaveChangesAsync();

        save.IsSuccess.Should().BeFalse();
        save.Message.Should().Contain("InventoryReorderRule");
        save.Message.Should().Contain("not registered for remote mutation");
    }
}
