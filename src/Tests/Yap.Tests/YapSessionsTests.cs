using System.Security.Claims;
using System.Net;
using Communications.Integration.Clients;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Yap.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Yap.Tests;

[TestFixture]
public sealed class YapSessionsTests
{
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.Unauthorized)]
    public async Task RefreshFailure_OnlyConfirmedRejectionRemovesSession(HttpStatusCode status)
    {
        var response = Session(); response.ExpiresIn = 1;
        var identity = new Mock<IIdentityServerServiceWrapper>();
        identity.Setup(i => i.RefreshToken(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<RefreshTokenResponse> { HttpStatusCode = status });
        using var provider = new ServiceCollection().AddSingleton(identity.Object).BuildServiceProvider();
        var (sessions, _) = Build(provider); var user = await sessions.CreateAsync(response);
        if (status == HttpStatusCode.Unauthorized)
            Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await sessions.GetActorAsync(user, default));
        else Assert.ThrowsAsync<HttpRequestException>(async () => await sessions.GetActorAsync(user, default));
        Assert.That(await sessions.ContainsAsync(user), Is.EqualTo(status != HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task BrowserDisconnectDuringRefresh_PersistsRotatedTokensForRecovery()
    {
        using var browser = new CancellationTokenSource();
        var response = Session(); response.ExpiresIn = 1;
        var identity = new Mock<IIdentityServerServiceWrapper>();
        identity.Setup(i => i.RefreshToken(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()))
            .Returns((RefreshTokenRequest _, CancellationToken refresh) =>
            {
                browser.Cancel();
                Assert.That(refresh.IsCancellationRequested, Is.False, "Browser crash must not cancel token persistence");
                return Task.FromResult(ChatFixture.Ok(new RefreshTokenResponse
                { SessionId = response.SessionId!.Value, AccessToken = "rotated", RefreshToken = "rotated-refresh", ExpiresIn = 1800 }));
            });
        using var provider = new ServiceCollection().AddSingleton(identity.Object).BuildServiceProvider();
        var (sessions, _) = Build(provider); var user = await sessions.CreateAsync(response);
        await sessions.GetActorAsync(user, browser.Token);
        Assert.That((await sessions.GetActorAsync(user, default)).AccessToken, Is.EqualTo("rotated"));
        identity.Verify(i => i.RefreshToken(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Create_TwoUsers_KeepActorTokensIsolatedAndOutOfCookieClaims()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var (sessions, cache) = Build(provider);
        var first = Session("first-token");
        var second = Session("second-token");
        var firstUser = await sessions.CreateAsync(first);
        var secondUser = await sessions.CreateAsync(second);
        Assert.Multiple(() =>
        {
            Assert.That(firstUser.Claims.Any(c => c.Value == first.AccessToken || c.Value == first.RefreshToken), Is.False);
            Assert.That(secondUser.FindFirstValue(YapAuth.SessionClaim), Is.Not.EqualTo(firstUser.FindFirstValue(YapAuth.SessionClaim)));
        });
        Assert.That((await sessions.GetActorAsync(firstUser, default)).AccessToken, Is.EqualTo("first-token"));
        Assert.That((await sessions.GetActorAsync(secondUser, default)).AccessToken, Is.EqualTo("second-token"));
    }

    [Test]
    public async Task GetActorAsync_MissingSession_RejectsInsteadOfReusingToken()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var (sessions, cache) = Build(provider);
        var user = await sessions.CreateAsync(Session());
        cache.Remove($"yap:session:{user.FindFirstValue(YapAuth.SessionClaim)}");
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await sessions.GetActorAsync(user, default));
    }

    [Test]
    public async Task GetActorAsync_ConcurrentRefresh_RefreshesOnce()
    {
        var response = Session();
        response.ExpiresIn = 1;
        var identity = new Mock<IIdentityServerServiceWrapper>();
        identity.Setup(i => i.RefreshToken(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new RefreshTokenResponse
            { SessionId = response.SessionId!.Value, AccessToken = "renewed", RefreshToken = "renewed-refresh", ExpiresIn = 1800 }));
        using var provider = new ServiceCollection().AddSingleton(identity.Object).BuildServiceProvider();
        var (sessions, cache) = Build(provider);
        var user = await sessions.CreateAsync(response);
        var actors = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => sessions.GetActorAsync(user, default).AsTask()));
        Assert.That(actors.All(a => a.AccessToken == "renewed"), Is.True);
        identity.Verify(i => i.RefreshToken(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RevokeAsync_UpstreamRejects_ReportsFailureAndStillRemovesLocalSession()
    {
        var response = Session();
        var identity = new Mock<IIdentityServerServiceWrapper>();
        identity.Setup(i => i.Logout(It.IsAny<LogoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.Forbidden });
        var tokens = new Mock<IActorAccessTokenScope>();
        tokens.Setup(t => t.Push(response.AccessToken!)).Returns(Mock.Of<IDisposable>());
        using var provider = new ServiceCollection().AddSingleton(identity.Object).AddSingleton(tokens.Object).BuildServiceProvider();
        var (sessions, cache) = Build(provider);
        var user = await sessions.CreateAsync(response);

        Assert.ThrowsAsync<ChatOperationException>(() => sessions.RevokeAsync(user, CancellationToken.None));

        Assert.That(await sessions.ContainsAsync(user), Is.False);
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await sessions.GetActorAsync(user, default));
        identity.Verify(i => i.Logout(It.Is<LogoutRequest>(r => r.SessionId == response.SessionId &&
            r.CredentialId == response.Credential!.Id && r.Metadata.RequestedTenantId == response.Credential.TenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetActorAsync_AfterHostRestart_KeepsTheSessionUsable()
    {
        // The reported "sign in to sync" interruption: the cookie stayed valid while an
        // in-process session store was emptied by a restart, so every call returned 401.
        // Data Protection keys persist across restarts, so the same provider is used twice.
        using var provider = new ServiceCollection().BuildServiceProvider();
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var protection = new ServiceCollection().AddDataProtection().Services
            .BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();

        var user = await new YapSessions(cache, protection, scopes, TimeProvider.System).CreateAsync(Session("kept-token"));
        var restarted = new YapSessions(cache, protection, scopes, TimeProvider.System);

        Assert.That(await restarted.ContainsAsync(user), Is.True);
        Assert.That((await restarted.GetActorAsync(user, default)).AccessToken, Is.EqualTo("kept-token"));
    }

    [Test]
    public async Task CreateAsync_SharedStoreHoldsCiphertext_NotTheActorTokens()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var (sessions, cache) = Build(provider);
        var response = Session("secret-access-token");

        var user = await sessions.CreateAsync(response);
        var stored = System.Text.Encoding.UTF8.GetString(
            cache.Get($"yap:session:{user.FindFirstValue(YapAuth.SessionClaim)}")!);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Does.Not.Contain(response.AccessToken!));
            Assert.That(stored, Does.Not.Contain(response.RefreshToken!));
        });
    }

    [Test]
    public async Task TouchAsync_ContinuedDailyUse_KeepsTheSignInAlive()
    {
        // The reported defect: a fixed timer signed people out mid-conversation. Use must roll.
        var clock = new Clock(DateTimeOffset.UtcNow);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var (sessions, _) = Build(provider, clock);
        var user = await sessions.CreateAsync(Session());

        for (var day = 1; day <= 20; day++)
        {
            clock.Advance(TimeSpan.FromDays(1));
            Assert.That(await sessions.TouchAsync(user), Is.Not.Null, $"Day {day} of daily use must not sign the user out");
        }
        Assert.That(await sessions.ContainsAsync(user), Is.True);
    }

    [Test]
    public async Task TouchAsync_IdleBeyondTheWindow_EndsTheSignIn()
    {
        var clock = new Clock(DateTimeOffset.UtcNow);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var (sessions, _) = Build(provider, clock);
        var user = await sessions.CreateAsync(Session());

        clock.Advance(TimeSpan.FromDays(6));
        Assert.That(await sessions.TouchAsync(user), Is.Not.Null, "Six idle days stay inside the idle window");
        // The window runs from that last use, not from sign-in.
        clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));
        Assert.That(await sessions.TouchAsync(user), Is.Null);
        Assert.That(await sessions.ContainsAsync(user), Is.False);
    }

    [Test]
    public async Task TouchAsync_ContinuousUse_StillEndsAtTheAbsoluteCap()
    {
        var clock = new Clock(DateTimeOffset.UtcNow);
        var start = clock.GetUtcNow();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var (sessions, _) = Build(provider, clock);
        var user = await sessions.CreateAsync(Session());

        for (var day = 1; day <= 27; day++)
        {
            clock.Advance(TimeSpan.FromDays(1));
            Assert.That(await sessions.TouchAsync(user), Is.Not.Null, $"Day {day} is inside the absolute cap");
        }
        Assert.That(await sessions.TouchAsync(user), Is.EqualTo(start.AddDays(28)), "Rolling must clamp to the cap");
        clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1));
        Assert.That(await sessions.TouchAsync(user), Is.Null, "A continuously used sign-in must still end at the cap");
    }

    [Test]
    public async Task RevokeAsync_RolledSession_StillEndsItImmediately()
    {
        // Rolling must never resurrect a sign-in that was deliberately ended.
        var clock = new Clock(DateTimeOffset.UtcNow);
        var response = Session();
        var identity = new Mock<IIdentityServerServiceWrapper>();
        identity.Setup(i => i.Logout(It.IsAny<LogoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        var tokens = new Mock<IActorAccessTokenScope>();
        tokens.Setup(t => t.Push(response.AccessToken!)).Returns(Mock.Of<IDisposable>());
        using var provider = new ServiceCollection().AddSingleton(identity.Object).AddSingleton(tokens.Object).BuildServiceProvider();
        var (sessions, cache) = Build(provider, clock);
        var user = await sessions.CreateAsync(response);

        clock.Advance(TimeSpan.FromDays(3));
        Assert.That(await sessions.TouchAsync(user), Is.Not.Null, "Three days of use keep it rolling");
        await sessions.RevokeAsync(user, CancellationToken.None);

        Assert.That(cache.Get($"yap:session:{user.FindFirstValue(YapAuth.SessionClaim)}"), Is.Null);
        Assert.That(await sessions.TouchAsync(user), Is.Null);
        Assert.That(await sessions.ContainsAsync(user), Is.False);
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await sessions.GetActorAsync(user, default));
        identity.Verify(i => i.Logout(It.IsAny<LogoutRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task TouchAsync_RollingDeadline_KeepsTheStoreTtlInStep()
    {
        // Redis drops the entry on its own TTL. A TTL that lags the logical deadline evicts
        // a session the app still honours; one that leads it keeps ciphertext past the sign-in.
        var clock = new Clock(DateTimeOffset.UtcNow);
        var cache = new TrackingCache();
        var protection = new ServiceCollection().AddDataProtection().Services
            .BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var sessions = new YapSessions(cache, protection, provider.GetRequiredService<IServiceScopeFactory>(), clock);
        var user = await sessions.CreateAsync(Session());
        var key = $"yap:session:{user.FindFirstValue(YapAuth.SessionClaim)}";

        Assert.That(cache.Ttl(key), Is.EqualTo(TimeSpan.FromDays(7)), "A fresh sign-in expires on the idle window");
        foreach (var day in (int[])[6, 12, 18])
        {
            clock.Advance(TimeSpan.FromDays(6));
            var rolled = await sessions.TouchAsync(user);
            Assert.That(cache.Ttl(key), Is.EqualTo(rolled - clock.GetUtcNow()), $"Day {day} TTL must track the rolled deadline");
        }
        clock.Advance(TimeSpan.FromDays(6));
        var capped = await sessions.TouchAsync(user);
        Assert.Multiple(() =>
        {
            Assert.That(cache.Ttl(key), Is.EqualTo(capped - clock.GetUtcNow()));
            Assert.That(cache.Ttl(key), Is.EqualTo(TimeSpan.FromDays(4)), "Near the cap the TTL follows the cap, not the idle window");
        });
    }

    /// <summary>Records the TTL each write asked for, which no in-memory cache exposes.</summary>
    private sealed class TrackingCache : IDistributedCache
    {
        private readonly MemoryDistributedCache inner = new(Options.Create(new MemoryDistributedCacheOptions()));
        private readonly Dictionary<string, TimeSpan?> ttls = [];

        public TimeSpan? Ttl(string key) => ttls.GetValueOrDefault(key);
        public byte[]? Get(string key) => inner.Get(key);
        public Task<byte[]?> GetAsync(string key, CancellationToken ct = default) => inner.GetAsync(key, ct);
        public void Refresh(string key) => inner.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken ct = default) => inner.RefreshAsync(key, ct);
        public void Remove(string key) { ttls.Remove(key); inner.Remove(key); }
        public Task RemoveAsync(string key, CancellationToken ct = default) { ttls.Remove(key); return inner.RemoveAsync(key, ct); }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            ttls[key] = options.AbsoluteExpirationRelativeToNow;
            inner.Set(key, value, options);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken ct = default)
        {
            ttls[key] = options.AbsoluteExpirationRelativeToNow;
            return inner.SetAsync(key, value, options, ct);
        }
    }

    private static (YapSessions Sessions, IDistributedCache Cache) Build(IServiceProvider provider, TimeProvider? clock = null)
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var protection = new ServiceCollection().AddDataProtection().Services
            .BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
        return (new YapSessions(cache, protection, provider.GetRequiredService<IServiceScopeFactory>(),
            clock ?? TimeProvider.System), cache);
    }

    /// <summary>A clock the test advances by hand, so week-scale session rules are testable.</summary>
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset utc = now;
        public override DateTimeOffset GetUtcNow() => utc;
        public Clock Advance(TimeSpan span) { utc = utc.Add(span); return this; }
    }

    internal static AuthenticateIdentityResponse Session(string token = "fixture-access-token") => new()
    {
        Credential = new AuthenticatedCredentialResponse { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), UserName = "Jamie Davis" },
        AccessToken = token, RefreshToken = token + "-refresh", SessionId = Guid.NewGuid(), ExpiresIn = 1800
    };
}
