using System;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using XFramework.Core.DataContext;
using XFramework.Core.Services;
using XFramework.Domain.Contexts;

namespace XFramework.Core.Tests.Services;

[TestFixture]
public sealed class TenantResolverTests
{
    [Test]
    public async Task GetTenant_CachedTenant_ReturnsCachedTenant()
    {
        await using var db = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "Cached tenant" };
        cache.Set($"GetTenant-{tenantId}", tenant);

        var resolver = new TenantResolver(new ServerDataContext<AppDbContext>(db), cache);

        var result = await resolver.GetTenant(tenantId);

        result.Should().BeSameAs(tenant);
    }

    [Test]
    public async Task GetTenant_ExistingTenant_ReturnsAndCachesTenant()
    {
        await using var db = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, TenantId = tenantId, Name = "Database tenant" };
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync();

        var resolver = new TenantResolver(new ServerDataContext<AppDbContext>(db), cache);

        var result = await resolver.GetTenant(tenantId);

        result.Id.Should().Be(tenantId);
        result.Name.Should().Be("Database tenant");
        cache.TryGetValue($"GetTenant-{tenantId}", out Tenant? cachedTenant).Should().BeTrue();
        cachedTenant.Should().NotBeNull();
        cachedTenant!.Id.Should().Be(tenantId);
    }

    [Test]
    public async Task GetTenant_MissingTenant_ThrowsInvalidOperationException()
    {
        await using var db = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new TenantResolver(new ServerDataContext<AppDbContext>(db), cache);

        Func<Task> act = () => resolver.GetTenant(Guid.NewGuid());

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*could not be found*");
    }

    [Test]
    public async Task GetTenant_EmptyTenantId_ThrowsArgumentNullException()
    {
        await using var db = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new TenantResolver(new ServerDataContext<AppDbContext>(db), cache);

        Func<Task> act = () => resolver.GetTenant(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static AppDbContext CreateDbContext()
    {
        _ = typeof(Tenant);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}
