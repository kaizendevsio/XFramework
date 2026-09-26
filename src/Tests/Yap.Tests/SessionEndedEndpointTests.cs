using System.Net;
using System.Net.Http.Json;
using Communications.Domain.Shared.Contracts.Responses;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using Yap.Contracts;

namespace Yap.Tests;

// The reported outage: an Identity session hit its old absolute expiry while its access token
// was still inside its lifetime. Every downstream call answered 401 "Identity session is no
// longer valid", the host passed that through without trying a refresh or ending the sign-in,
// and the browser, still seeing a live cookie, showed "Can't reach Yap" for eight minutes.
public sealed class SessionEndedEndpointTests
{
    // The wire contract the browser reads. Literal on purpose: renaming it breaks old clients.
    private const string Signal = "X-Yap-Session";

    [Test]
    public async Task DownstreamRefusal_WithRefreshRefused_EndsTheSignInOnTheFirstRequest()
    {
        Mock<IIdentityServerServiceWrapper> identity = null!;
        await using var app = UiFixture.Create(0,
            configureIdentity: mock =>
            {
                identity = mock;
                mock.Setup(i => i.RefreshToken(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new QueryResponse<RefreshTokenResponse> { HttpStatusCode = HttpStatusCode.Unauthorized });
            },
            configureChat: chat =>
            {
                chat.Session.Setup(s => s.EnsureChatDefaultsAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new QueryResponse<ChatReferenceDataResponse> { HttpStatusCode = HttpStatusCode.Unauthorized });
                chat.Session.Setup(s => s.GetThreadsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new QueryResponse<GetThreadListResponse> { HttpStatusCode = HttpStatusCode.Unauthorized });
            });
        await app.StartAsync();
        using var client = await SignedInAsync(app);

        using var initialize = await client.PostAsJsonAsync("api/chat/initialize", new { });

        Assert.That(initialize.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), await initialize.Content.ReadAsStringAsync());
        Assert.That(initialize.Headers.TryGetValues(Signal, out var signal) ? signal : [], Is.EqualTo(new[] { "ended" }),
            "The first refusal must reach the browser as an ended sign-in, not a bare 401.");
        identity.Verify(i => i.RefreshToken(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()), Times.Once,
            "A refusal is settled by one refresh straight away, not when the access token lapses.");
        Assert.That((await client.GetFromJsonAsync<SessionResponse>("api/session"))!.User, Is.Null,
            "A refused refresh removes the sign-in, so the browser's own check agrees it ended.");
        using var next = await client.GetAsync("api/chat/conversations");
        Assert.That(next.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(next.Headers.TryGetValues(Signal, out var again) ? again : [], Is.EqualTo(new[] { "ended" }));
        identity.Verify(i => i.RefreshToken(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()), Times.Once,
            "An ended sign-in stops reaching IdentityServer.");
        await app.StopAsync();
    }

    // Only a refused refresh ends a sign-in (#552). A refresh that rotated, or could not be
    // reached, leaves it alone and answers retryably, as a reconnect rather than a sign-out.
    [TestCase(HttpStatusCode.OK)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    public async Task DownstreamRefusal_WithRefreshNotRefused_KeepsTheSignIn(HttpStatusCode refresh)
    {
        await using var app = UiFixture.Create(0,
            configureIdentity: mock => mock.Setup(i => i.RefreshToken(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((RefreshTokenRequest request, CancellationToken _) => refresh == HttpStatusCode.OK
                    ? ChatFixture.Ok(new RefreshTokenResponse { SessionId = request.SessionId, AccessToken = "rotated", RefreshToken = "rotated-refresh", ExpiresIn = 1800 })
                    : new QueryResponse<RefreshTokenResponse> { HttpStatusCode = refresh }),
            configureChat: chat => chat.Session.Setup(s => s.EnsureChatDefaultsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryResponse<ChatReferenceDataResponse> { HttpStatusCode = HttpStatusCode.Unauthorized }));
        await app.StartAsync();
        using var client = await SignedInAsync(app);

        using var initialize = await client.PostAsJsonAsync("api/chat/initialize", new { });

        Assert.That(initialize.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(initialize.Headers.Contains(Signal), Is.False);
        Assert.That((await client.GetFromJsonAsync<SessionResponse>("api/session"))!.User, Is.Not.Null);
        await app.StopAsync();
    }

    private static async Task<HttpClient> SignedInAsync(Microsoft.AspNetCore.Builder.WebApplication app)
    {
        var handler = new HttpClientHandler { CookieContainer = new() };
        var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        using var login = await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        login.EnsureSuccessStatusCode();
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        // Antiforgery tokens are bound to the signed-in identity.
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{session.User.CredentialId:N}");
        return client;
    }
}
