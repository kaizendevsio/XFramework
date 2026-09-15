using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Yap.Services;

namespace Yap.Tests;

public sealed class YapPresenceTests
{
    [Test]
    public async Task Heartbeats_AreTenantScoped_Expire_AndCanBeRenewed()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var clock = new Clock(); var presence = new YapPresence(cache, clock);
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid();
        await presence.TouchAsync(tenant, user, default);
        Assert.That(await presence.ActiveUntilAsync(tenant, user, default), Is.EqualTo(clock.Utc.Add(YapPresence.Lifetime).UtcDateTime));
        Assert.That(await presence.ActiveUntilAsync(Guid.NewGuid(), user, default), Is.Null);
        var last = clock.Utc.UtcDateTime;
        clock.Utc += TimeSpan.FromSeconds(46);
        Assert.That(await presence.ActiveUntilAsync(tenant, user, default), Is.Null);
        Assert.That(await presence.LastActiveAtAsync(tenant, user, default), Is.EqualTo(last));
        clock.Utc += TimeSpan.FromDays(1);
        Assert.That(await presence.LastActiveAtAsync(tenant, user, default), Is.Null);
        await presence.TouchAsync(tenant, user, default);
        Assert.That(await presence.ActiveUntilAsync(tenant, user, default), Is.Not.Null);
    }

    [Test]
    public async Task CacheOutage_DegradesToUnknownPresence()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());
        var presence = new YapPresence(cache.Object, new Clock());
        await presence.TouchAsync(Guid.NewGuid(), Guid.NewGuid(), default);
        Assert.That(await presence.ActiveUntilAsync(Guid.NewGuid(), Guid.NewGuid(), default), Is.Null);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Utc = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000);
        public override DateTimeOffset GetUtcNow() => Utc;
    }
}
