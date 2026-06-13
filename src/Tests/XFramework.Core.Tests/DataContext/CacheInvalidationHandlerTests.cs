using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Integration.DataContext.Cache;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public sealed class CacheInvalidationHandlerTests
{
    [Test]
    public async Task InvalidatePrefixAsync_RemovesMatchingPrefixFromClientCache()
    {
        var cache = new Mock<IClientCacheService>();
        var handler = new CacheInvalidationHandler(cache.Object, NullLogger<CacheInvalidationHandler>.Instance);

        await handler.InvalidatePrefixAsync("Tenant:");

        handler.ServerPushInvalidationEnabled.Should().BeFalse();
        cache.Verify(x => x.RemoveByPrefixAsync("Tenant:", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task InvalidatePrefixAsync_BlankPrefix_ThrowsArgumentException()
    {
        var cache = new Mock<IClientCacheService>();
        var handler = new CacheInvalidationHandler(cache.Object, NullLogger<CacheInvalidationHandler>.Instance);

        Func<Task> act = () => handler.InvalidatePrefixAsync(" ");

        await act.Should().ThrowAsync<ArgumentException>();
        cache.Verify(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ClearAllAsync_ClearsClientCache()
    {
        var cache = new Mock<IClientCacheService>();
        var handler = new CacheInvalidationHandler(cache.Object, NullLogger<CacheInvalidationHandler>.Instance);

        await handler.ClearAllAsync();

        cache.Verify(x => x.ClearAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
