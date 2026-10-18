using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using Yap.Contracts;
using Yap.Services;

namespace Yap.Tests;

[TestFixture]
public sealed class TenantRoutingEndpointTests
{
    private static readonly Guid BetaTenant = Guid.NewGuid();
    private static readonly Guid AlphaRole = Guid.NewGuid();
    private static readonly Guid BetaRole = Guid.NewGuid();

    [Test]
    public async Task TwoHosts_BindLoginAndOpaqueToConfiguredTenantAndRole_AndExposeOnlyPublicBranding()
    {
        Mock<IIdentityServerServiceWrapper>? identity = null;
        await using var app = UiFixture.Create(0, mock =>
        {
            identity = mock;
            mock.Setup(x => x.AuthenticateIdentity(It.IsAny<AuthenticateIdentityRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AuthenticateIdentityRequest request, CancellationToken _) => ChatFixture.Ok(Authentication(request.Metadata.RequestedTenantId!.Value)));
            mock.Setup(x => x.OpaqueAuth(It.IsAny<OpaqueAuthRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((OpaqueAuthRequest request, CancellationToken _) => ChatFixture.Ok(new OpaqueAuthResponse
                { Mode = "opaque", Client = "public-protocol-client", Authentication = Authentication(request.Metadata.RequestedTenantId!.Value) }));
        }, configureApplication: ConfigureHosts);
        await app.StartAsync();
        using var alpha = Client(app, "alpha.dev.localhost");
        using var beta = Client(app, "beta.dev.localhost");
        beta.DefaultRequestHeaders.Add("X-Forwarded-Host", "alpha.dev.localhost");

        var brand = await alpha.GetFromJsonAsync<YapBranding>("/api/branding");
        Assert.That(brand!.Name, Is.EqualTo("Alpha Chat"));
        Assert.That(brand.ManifestUrl, Is.EqualTo("/api/branding/manifest"));
        var betaJson = await beta.GetStringAsync("/api/branding");
        Assert.That(betaJson, Does.Contain("Beta Chat").And.Not.Contain("tenantId").And.Not.Contain("roleId").And.Not.Contain("fixture-only-secret"));

        var alphaSession = await alpha.GetFromJsonAsync<SessionResponse>("/api/session");
        var login = await alpha.PostAsync("/api/auth/login", Form(alphaSession!.AntiforgeryToken));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await alpha.GetFromJsonAsync<SessionResponse>("/api/session"))!.User!.TenantId,
            Is.EqualTo(Guid.Parse(app.Configuration["Yap:TenantId"]!)));

        var betaSession = await beta.GetFromJsonAsync<SessionResponse>("/api/session");
        beta.DefaultRequestHeaders.Add("RequestVerificationToken", betaSession!.AntiforgeryToken);
        var opaque = await beta.PostAsJsonAsync("/api/auth/opaque", new OpaqueAuthRequest
        { Stage = "login-finish", UserName = "fixture", RoleId = AlphaRole, Metadata = new() { RequestedTenantId = Guid.Parse(app.Configuration["Yap:TenantId"]!) } });
        Assert.That(opaque.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await opaque.Content.ReadAsStringAsync(), Does.Not.Contain("fixture-access-token"));
        Assert.That((await beta.GetFromJsonAsync<SessionResponse>("/api/session"))!.User!.TenantId, Is.EqualTo(BetaTenant));
        var passwordRequest = identity!.Invocations.Select(i => i.Arguments[0]).OfType<AuthenticateIdentityRequest>().Single();
        Assert.That(passwordRequest.RoleId, Is.EqualTo(AlphaRole));
        Assert.That(passwordRequest.Metadata.RequestedTenantId, Is.EqualTo(Guid.Parse(app.Configuration["Yap:TenantId"]!)));
        var opaqueRequest = identity.Invocations.Select(i => i.Arguments[0]).OfType<OpaqueAuthRequest>().Single();
        Assert.That(opaqueRequest.RoleId, Is.EqualTo(BetaRole));
        Assert.That(opaqueRequest.Metadata.RequestedTenantId, Is.EqualTo(BetaTenant));

        using var manifest = JsonDocument.Parse(await beta.GetStringAsync("/api/branding/manifest"));
        Assert.That(manifest.RootElement.GetProperty("name").GetString(), Is.EqualTo("Beta Chat"));
        Assert.That(manifest.RootElement.GetProperty("theme_color").GetString(), Is.EqualTo("#336699"));
        Assert.That(manifest.RootElement.GetProperty("icons")[0].GetProperty("src").GetString(), Is.EqualTo("/yap-app-v2-192.png"));
        var response = await beta.GetAsync("/api/branding/manifest");
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/manifest+json"));
        Assert.That(response.Headers.CacheControl!.NoStore, Is.True);
        Assert.That(await beta.GetStringAsync("/login"), Does.Contain("branding.js"));
    }

    [Test]
    public async Task WrongTenantAuthenticationResponse_CannotCreatePasswordOrOpaqueSession()
    {
        var wrong = Authentication(Guid.NewGuid());
        await using var app = UiFixture.Create(0, mock =>
        {
            mock.Setup(x => x.AuthenticateIdentity(It.IsAny<AuthenticateIdentityRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChatFixture.Ok(wrong));
            mock.Setup(x => x.OpaqueAuth(It.IsAny<OpaqueAuthRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChatFixture.Ok(new OpaqueAuthResponse { Mode = "opaque", Authentication = wrong }));
        }, configureApplication: ConfigureHosts);
        await app.StartAsync();
        using var beta = Client(app, "beta.dev.localhost");
        var session = await beta.GetFromJsonAsync<SessionResponse>("/api/session");
        var password = await beta.PostAsync("/api/auth/login", Form(session!.AntiforgeryToken));
        Assert.That(await password.Content.ReadAsStringAsync(), Does.Contain("/login?error=credentials"));
        Assert.That(password.Headers.Contains("Set-Cookie"), Is.False);
        beta.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        var opaque = await beta.PostAsJsonAsync("/api/auth/opaque", new OpaqueAuthRequest { Stage = "login-finish" });
        Assert.That(opaque.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(opaque.Headers.Contains("Set-Cookie"), Is.False);
        Assert.That((await beta.GetFromJsonAsync<SessionResponse>("/api/session"))!.User, Is.Null);
    }

    [Test]
    public async Task AuthenticatedCookie_OnAnotherTenantHost_IsRejectedBeforeProtectedHttpOpaqueAndSocketUpgrade()
    {
        Mock<IIdentityServerServiceWrapper>? identity = null;
        await using var app = UiFixture.Create(0, mock => identity = mock, configureApplication: ConfigureHosts);
        await app.StartAsync();
        using var alpha = Client(app, "alpha.dev.localhost");
        var session = await alpha.GetFromJsonAsync<SessionResponse>("/api/session");
        var login = await alpha.PostAsync("/api/auth/login", Form(session!.AntiforgeryToken));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var cookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("Yap.Session=", StringComparison.Ordinal)).Split(';')[0];
        using var handler = new HttpClientHandler { UseCookies = false };
        using var beta = new HttpClient(handler) { BaseAddress = alpha.BaseAddress };
        beta.DefaultRequestHeaders.Host = "beta.dev.localhost";
        beta.DefaultRequestHeaders.Add("Cookie", cookie);
        beta.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        foreach (var route in new[] { "/api/session", "/api/chat/conversations", "/api/chat/socket?ticket=irrelevant", "/api/chat/calls/socket?ticket=irrelevant" })
            Assert.That((await beta.GetAsync(route)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), route);
        Assert.That((await beta.PostAsJsonAsync("/api/auth/opaque", new OpaqueAuthRequest { Stage = "status" })).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        identity!.Verify(x => x.OpaqueAuth(It.IsAny<OpaqueAuthRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        // Rejecting crossover must not revoke or touch the legitimate original host session.
        Assert.That((await alpha.GetFromJsonAsync<SessionResponse>("/api/session"))!.User, Is.Not.Null);
    }

    [Test]
    public async Task MappedMode_UnknownHostFailsClosed_AndForwardedHostCannotSelectTenant()
    {
        Mock<IIdentityServerServiceWrapper>? identity = null;
        await using var app = UiFixture.Create(0, mock => identity = mock, configureApplication: ConfigureHosts);
        await app.StartAsync();
        using var client = Client(app, "unknown.dev.localhost");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "alpha.dev.localhost");
        foreach (var path in new[] { "/login", "/api/branding", "/api/session", "/api/branding/manifest", "/manifest.webmanifest", "/api/chat/socket" })
            Assert.That((await client.GetAsync(path)).StatusCode, Is.EqualTo(HttpStatusCode.MisdirectedRequest), path);
        Assert.That((await client.PostAsync("/api/auth/login", Form("untrusted"))).StatusCode, Is.EqualTo(HttpStatusCode.MisdirectedRequest));
        identity!.Verify(x => x.AuthenticateIdentity(It.IsAny<AuthenticateIdentityRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That((await client.GetAsync("/health/live")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await client.GetStringAsync("/health/live"), Does.Contain("Healthy"));
    }

    [Test]
    public async Task MappedOnlyConfiguration_DoesNotRequireLegacyTenantOrRoleForReadiness()
    {
        await using var app = UiFixture.Create(0, configureApplication: builder =>
        {
            ConfigureHosts(builder);
            builder.Configuration["Yap:TenantId"] = "";
            builder.Configuration["Yap:RoleId"] = "";
        });
        await app.StartAsync();
        using var client = Client(app, "alpha.dev.localhost");
        Assert.That((await client.GetAsync("/api/branding")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ready = await client.GetAsync("/health/ready");
        Assert.That(ready.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(await ready.Content.ReadAsStringAsync(), Does.Contain("Bolt is disconnected").And.Not.Contain("Workspace is not configured"));
    }

    [Test]
    public async Task LegacyMode_KeepsFixedTenantAndStaticManifest()
    {
        await using var app = UiFixture.Create(0);
        await app.StartAsync();
        using var client = Client(app, "legacy.dev.localhost");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "unknown.dev.localhost");
        Assert.That(await client.GetFromJsonAsync<YapBranding>("/api/branding"), Is.EqualTo(YapBranding.Default));
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/session");
        Assert.That((await client.PostAsync("/api/auth/login", Form(session!.AntiforgeryToken))).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await client.GetFromJsonAsync<SessionResponse>("/api/session"))!.User!.TenantId, Is.EqualTo(Guid.Parse(app.Configuration["Yap:TenantId"]!)));
        using var manifest = JsonDocument.Parse(await client.GetStringAsync("/manifest.webmanifest"));
        Assert.That(manifest.RootElement.GetProperty("name").GetString(), Is.EqualTo("Yap"));
    }

    [TestCase("Yap:Hosts:alpha.dev.localhost:TenantId", "00000000-0000-0000-0000-000000000000")]
    [TestCase("Yap:Hosts:alpha.dev.localhost:RoleId", "invalid")]
    [TestCase("Yap:Hosts:alpha.dev.localhost:Branding:Name", "")]
    [TestCase("Yap:Hosts:alpha.dev.localhost:Branding:AccentColor", "red;display:none")]
    [TestCase("Yap:Hosts:alpha.dev.localhost:Branding:LogoUrl", "https://other.example/logo.png")]
    [TestCase("Yap:Hosts:alpha.dev.localhost:Branding:LogoUrl", "/../logo.png")]
    [TestCase("Yap:Hosts:alpha.dev.localhost:Branding:LogoUrl", "/logo.png?secret=true")]
    [TestCase("Yap:Hosts", "invalid")]
    public void InvalidMappings_FailAtStartup(string key, string value) =>
        Assert.Throws<InvalidOperationException>(() => UiFixture.Create(0, configureApplication: builder =>
        { ConfigureHosts(builder); builder.Configuration[key] = value; }));

    [TestCase("*.dev.localhost")]
    [TestCase("https://alpha.dev.localhost")]
    [TestCase("alpha.dev.localhost:443")]
    public void NonExactHostMapping_FailsAtStartup(string host) => Assert.Throws<InvalidOperationException>(() =>
        new YapTenants(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [$"Yap:Hosts:{host}:TenantId"] = BetaTenant.ToString(), [$"Yap:Hosts:{host}:RoleId"] = BetaRole.ToString() }).Build()));

    private static AuthenticateIdentityResponse Authentication(Guid tenant)
    {
        var authentication = YapSessionsTests.Session();
        authentication.Credential!.TenantId = tenant;
        return authentication;
    }

    private static void ConfigureHosts(WebApplicationBuilder builder)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Yap:Hosts:alpha.dev.localhost:TenantId"] = builder.Configuration["Yap:TenantId"],
            ["Yap:Hosts:alpha.dev.localhost:RoleId"] = AlphaRole.ToString(),
            ["Yap:Hosts:alpha.dev.localhost:Branding:Name"] = "Alpha Chat",
            ["Yap:Hosts:alpha.dev.localhost:Branding:LogoUrl"] = "/yap-app-v2.svg",
            ["Yap:Hosts:beta.dev.localhost:TenantId"] = BetaTenant.ToString(),
            ["Yap:Hosts:beta.dev.localhost:RoleId"] = BetaRole.ToString(),
            ["Yap:Hosts:beta.dev.localhost:Branding:Name"] = "Beta Chat",
            ["Yap:Hosts:beta.dev.localhost:Branding:AccentColor"] = "#336699"
        });
    }

    private static HttpClient Client(WebApplication app, string host)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Host = host;
        return client;
    }

    private static FormUrlEncodedContent Form(string token) => new(new Dictionary<string, string>
    { ["username"] = "fixture", ["password"] = "fixture", ["__RequestVerificationToken"] = token });
}
