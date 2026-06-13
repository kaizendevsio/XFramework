using System;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using XFramework.Core.Services;

namespace XFramework.Core.Tests.Services;

[TestFixture]
public sealed class TenantResolverTests
{
    [Test]
    public async Task GetTenant_CachedTenant_ReturnsCachedTenant()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "Cached tenant" };
        cache.Set($"GetTenant-{tenantId}", tenant);

        var resolver = new TenantResolver(cache);

        var result = await resolver.GetTenant(tenantId);

        result.Should().BeSameAs(tenant);
    }

    [Test]
    public async Task GetTenant_MissingTenant_ThrowsExplicitUnsupportedError()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new TenantResolver(cache);

        Func<Task> act = () => resolver.GetTenant(Guid.NewGuid());

        await act.Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*Tenant lookup is not supported*");
    }

    [Test]
    public async Task GetTenant_EmptyTenantId_ThrowsArgumentNullException()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new TenantResolver(cache);

        Func<Task> act = () => resolver.GetTenant(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
