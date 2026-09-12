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

        var user = await new YapSessions(cache, protection, scopes).CreateAsync(Session("kept-token"));
        var restarted = new YapSessions(cache, protection, scopes);

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

    private static (YapSessions Sessions, IDistributedCache Cache) Build(IServiceProvider provider)
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var protection = new ServiceCollection().AddDataProtection().Services
            .BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
        return (new YapSessions(cache, protection, provider.GetRequiredService<IServiceScopeFactory>()), cache);
    }

    internal static AuthenticateIdentityResponse Session(string token = "fixture-access-token") => new()
    {
        Credential = new AuthenticatedCredentialResponse { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), UserName = "Jamie Davis" },
        AccessToken = token, RefreshToken = token + "-refresh", SessionId = Guid.NewGuid(), ExpiresIn = 1800
    };
}
