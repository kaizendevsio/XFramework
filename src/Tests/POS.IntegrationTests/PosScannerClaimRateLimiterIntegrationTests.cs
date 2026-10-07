using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using POS.Api.Services;
using StackExchange.Redis;
using XFramework.Core.RateLimiting;

namespace POS.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("Kind:Integration")]
[Category("Module:POS")]
[Category("Area:Scanner")]
public sealed class PosScannerClaimRateLimiterIntegrationTests
{
    private IContainer redis = null!;
    private ConnectionMultiplexer first = null!;
    private ConnectionMultiplexer second = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        redis = new ContainerBuilder("redis:7.4-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();
        await redis.StartAsync();
        var endpoint = $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}";
        first = await ConnectionMultiplexer.ConnectAsync(endpoint);
        second = await ConnectionMultiplexer.ConnectAsync(endpoint);
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        if (first is not null) await first.DisposeAsync();
        if (second is not null) await second.DisposeAsync();
        if (redis is not null) await redis.DisposeAsync();
    }

    [Test]
    public async Task ActorBudget_ConcurrentConnectionsShareExactlyFivePermits()
    {
        var policy = new StrictSecurityRateLimitPolicy("scanner-actor-test-" + Guid.NewGuid().ToString("N"), 5, TimeSpan.FromMinutes(2));
        var actor = Guid.NewGuid().ToString("N");
        var limiters = new[] { new PosScannerClaimRateLimiter(() => first), new PosScannerClaimRateLimiter(() => second) };
        var results = await Task.WhenAll(Enumerable.Range(0,20)
            .Select(i => limiters[i % 2].AcquireAsync(policy, actor, CancellationToken.None).AsTask()));
        results.Count(r => r.IsAllowed).Should().Be(5);
        results.Count(r => !r.IsAllowed).Should().Be(15);
        results.Where(r => !r.IsAllowed).Should().OnlyContain(r => r.RetryAfter > TimeSpan.Zero && r.RetryAfter <= policy.Window);
        (await limiters[1].AcquireAsync(policy, Guid.NewGuid().ToString("N"), CancellationToken.None)).IsAllowed.Should().BeTrue();
    }

    [Test]
    public async Task GlobalBudget_ConnectionsShareFiveHundredPermits_AndOnlyOneExpiringKey()
    {
        var policy = new StrictSecurityRateLimitPolicy("scanner-global-test-" + Guid.NewGuid().ToString("N"), 500, TimeSpan.FromMinutes(2));
        var limiters = new[] { new PosScannerClaimRateLimiter(() => first), new PosScannerClaimRateLimiter(() => second) };
        var results = await Task.WhenAll(Enumerable.Range(0,501)
            .Select(i => limiters[i % 2].AcquireAsync(policy, "all", CancellationToken.None).AsTask()));
        results.Count(r => r.IsAllowed).Should().Be(500);
        var keys = first.GetServer(first.GetEndPoints().Single()).Keys(pattern:$"xframework:pos:scanner:claims:{policy.Name}:*").ToArray();
        keys.Should().ContainSingle();
        (await first.GetDatabase().KeyTimeToLiveAsync(keys.Single())).Should().BePositive().And.BeLessThanOrEqualTo(policy.Window);
    }

    [Test]
    public async Task ExpiredBudget_RedisTtlAllowsNewAttemptFromOtherConnection()
    {
        var policy = new StrictSecurityRateLimitPolicy("scanner-expiry-test-" + Guid.NewGuid().ToString("N"), 1, TimeSpan.FromMilliseconds(250));
        var limiter = new PosScannerClaimRateLimiter(() => first);
        (await limiter.AcquireAsync(policy,"actor",CancellationToken.None)).IsAllowed.Should().BeTrue();
        (await new PosScannerClaimRateLimiter(() => second).AcquireAsync(policy,"actor",CancellationToken.None)).IsAllowed.Should().BeFalse();
        await Task.Delay(400);
        (await new PosScannerClaimRateLimiter(() => second).AcquireAsync(policy,"actor",CancellationToken.None)).IsAllowed.Should().BeTrue();
    }
}
