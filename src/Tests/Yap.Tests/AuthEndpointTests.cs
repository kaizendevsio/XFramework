using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace Yap.Tests;

[TestFixture]
public sealed class AuthEndpointTests
{
    [Test]
    public async Task LoginAndLogout_RequireAntiforgeryAndIssueOnlyAuthenticatedSession()
    {
        await using var app = UiFixture.Create(0);
        await app.StartAsync();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
        var anonymous = await client.GetAsync("/");
        Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        var rejected = await client.PostAsync("/auth/login", Form(null));
        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var page = await client.GetStringAsync("/login");
        var login = await client.PostAsync("/auth/login", Form(Token(page)));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(login.Headers.Location?.OriginalString, Is.EqualTo("/"));
        var settings = await client.GetStringAsync("/settings");
        Assert.That(settings, Does.Contain("Jamie Davis"));
        Assert.That(settings, Does.Not.Contain("fixture-access-token"));
        var logoutWithoutToken = await client.PostAsync("/auth/logout", new FormUrlEncodedContent([]));
        Assert.That(logoutWithoutToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var logout = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = Token(settings) }));
        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That((await client.GetAsync("/settings")).StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        await app.StopAsync();
    }

    private static FormUrlEncodedContent Form(string? token)
    {
        var values = new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" };
        if (token is not null) values["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(values);
    }
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value);
}
