using System.Net;
using System.Net.Http.Json;
using Yap.Contracts;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace Yap.Tests;

[TestFixture]
public sealed class AuthEndpointTests
{
    [Test]
    public async Task Readiness_RejectsDisconnectedBoltWhileLivenessRemainsHealthy()
    {
        await using var app = UiFixture.Create(0);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        Assert.That((await client.GetAsync("/health/live")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ready = await client.GetAsync("/health/ready");
        Assert.That(ready.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(await ready.Content.ReadAsStringAsync(), Does.Contain("Bolt is disconnected"));
        await app.StopAsync();
    }

    [Test]
    public async Task LoginAndLogout_RequireAntiforgeryAndIssueOnlyAuthenticatedSession()
    {
        await using var app = UiFixture.Create(0);
        await app.StartAsync();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
        var anonymous = await client.GetAsync("/api/chat/conversations");
        Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var rejected = await client.PostAsync("/auth/login", Form(null));
        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var page = await client.GetFromJsonAsync<SessionResponse>("/api/session");
        var login = await client.PostAsync("/auth/login", Form(page!.AntiforgeryToken));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(login.Headers.Location?.OriginalString, Is.EqualTo("/"));
        var settings = await client.GetStringAsync("/api/session");
        Assert.That(settings, Does.Contain("Jamie Davis"));
        Assert.That(settings, Does.Not.Contain("fixture-access-token"));
        var logoutWithoutToken = await client.PostAsync("/auth/logout", new FormUrlEncodedContent([]));
        Assert.That(logoutWithoutToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var authenticated = await client.GetFromJsonAsync<SessionResponse>("/api/session");
        var logout = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = authenticated!.AntiforgeryToken }));
        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That((await client.GetAsync("/api/chat/conversations")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        await app.StopAsync();
    }

    private static FormUrlEncodedContent Form(string? token)
    {
        var values = new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" };
        if (token is not null) values["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(values);
    }
}
