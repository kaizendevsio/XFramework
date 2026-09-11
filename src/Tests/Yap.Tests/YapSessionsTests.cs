using System.Security.Claims;
using System.Net;
using Communications.Integration.Clients;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.Extensions.Caching.Memory;
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
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sessions = new YapSessions(cache, provider.GetRequiredService<IServiceScopeFactory>());
        var first = Session("first-token");
        var second = Session("second-token");
        var firstUser = sessions.Create(first);
        var secondUser = sessions.Create(second);
        Assert.Multiple(() =>
        {
            Assert.That(firstUser.Claims.Any(c => c.Value == first.AccessToken || c.Value == first.RefreshToken), Is.False);
            Assert.That(secondUser.FindFirstValue(YapAuth.SessionClaim), Is.Not.EqualTo(firstUser.FindFirstValue(YapAuth.SessionClaim)));
        });
        Assert.That((await sessions.GetActorAsync(firstUser, default)).AccessToken, Is.EqualTo("first-token"));
        Assert.That((await sessions.GetActorAsync(secondUser, default)).AccessToken, Is.EqualTo("second-token"));
    }

    [Test]
    public void GetActorAsync_MissingSession_RejectsInsteadOfReusingToken()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sessions = new YapSessions(cache, provider.GetRequiredService<IServiceScopeFactory>());
        var user = sessions.Create(Session());
        cache.Remove(user.FindFirstValue(YapAuth.SessionClaim)!);
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
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sessions = new YapSessions(cache, provider.GetRequiredService<IServiceScopeFactory>());
        var user = sessions.Create(response);
        var actors = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => sessions.GetActorAsync(user, default).AsTask()));
        Assert.That(actors.All(a => a.AccessToken == "renewed"), Is.True);
        identity.Verify(i => i.RefreshToken(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void RevokeAsync_UpstreamRejects_ReportsFailureAndStillRemovesLocalSession()
    {
        var response = Session();
        var identity = new Mock<IIdentityServerServiceWrapper>();
        identity.Setup(i => i.Logout(It.IsAny<LogoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.Forbidden });
        var tokens = new Mock<IActorAccessTokenScope>();
        tokens.Setup(t => t.Push(response.AccessToken!)).Returns(Mock.Of<IDisposable>());
        using var provider = new ServiceCollection().AddSingleton(identity.Object).AddSingleton(tokens.Object).BuildServiceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sessions = new YapSessions(cache, provider.GetRequiredService<IServiceScopeFactory>());
        var user = sessions.Create(response);

        Assert.ThrowsAsync<ChatOperationException>(() => sessions.RevokeAsync(user, CancellationToken.None));

        Assert.That(sessions.Contains(user), Is.False);
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await sessions.GetActorAsync(user, default));
        identity.Verify(i => i.Logout(It.Is<LogoutRequest>(r => r.SessionId == response.SessionId &&
            r.CredentialId == response.Credential!.Id && r.Metadata.RequestedTenantId == response.Credential.TenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    internal static AuthenticateIdentityResponse Session(string token = "fixture-access-token") => new()
    {
        Credential = new AuthenticatedCredentialResponse { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), UserName = "Jamie Davis" },
        AccessToken = token, RefreshToken = token + "-refresh", SessionId = Guid.NewGuid(), ExpiresIn = 1800
    };
}
