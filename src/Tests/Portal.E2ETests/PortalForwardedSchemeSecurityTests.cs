using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XFramework.Portal.Extensions;

namespace Portal.E2ETests;

[TestFixture]
[Category("Kind:Unit")]
[Category("Area:Security")]
[Category("Area:PortalContract")]
public sealed class PortalForwardedSchemeSecurityTests
{
    [TestCase("192.0.2.10", "https", "https", true)]
    [TestCase("::ffff:192.0.2.10", "https", "https", true)]
    [TestCase("192.0.2.11", "https", "http", false)]
    [TestCase("192.0.2.10", null, "http", false)]
    [TestCase("192.0.2.10", "http", "http", false)]
    [TestCase("192.0.2.10", "https, http", "http", false)]
    public async Task ForwardedScheme_OnlyTrustedLastHopControlsSchemeAndCookie(
        string peer, string? forwardedProto, string expectedScheme, bool secure)
    {
        using var services = CreateServices("192.0.2.10");
        var context = CreateContext(services, peer, forwardedProto);
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.50";
        context.Request.Headers["X-Forwarded-Host"] = "attacker.invalid";

        await CreatePipeline(services)(context);

        context.Request.Scheme.Should().Be(expectedScheme);
        context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse(peer));
        context.Request.Host.Value.Should().Be("portal.test:5000");
        var cookie = context.Response.Headers.SetCookie.ToString();
        cookie.Should().Contain("XFramework.Portal=").And.Contain("httponly");
        cookie.Split(';').Any(value => value.Trim().Equals("secure", StringComparison.OrdinalIgnoreCase))
            .Should().Be(secure);
    }

    [Test]
    public async Task ForwardedScheme_NoConfiguredProxy_IgnoresEvenLoopbackHeader()
    {
        using var services = CreateServices(null);
        var context = CreateContext(services, "127.0.0.1", "https");

        await CreatePipeline(services)(context);

        context.Request.Scheme.Should().Be("http");
        services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value.ForwardedHeaders
            .Should().Be(ForwardedHeaders.None);
    }

    [TestCase("192.0.2.10", "https", 200)]
    [TestCase("192.0.2.10", null, 307)]
    [TestCase("192.0.2.11", "https", 307)]
    public async Task ForwardedScheme_BeforeHttpsRedirection_PreservesDirectHttpRedirect(
        string peer, string? proto, int expectedStatus)
    {
        using var services = CreateServices("192.0.2.10");
        var context = CreateContext(services, peer, proto);

        await CreatePipeline(services, redirect: true)(context);

        context.Response.StatusCode.Should().Be(expectedStatus);
        if (expectedStatus == 307)
            context.Response.Headers.Location.ToString().Should().Be("https://portal.test:8443/");
        else
            context.Response.Headers.SetCookie.ToString().Should().Contain("secure");
    }

    [TestCase("proxy.invalid")]
    [TestCase("0.0.0.0")]
    [TestCase("::")]
    public void ForwardedScheme_InvalidProxy_FailsClosed(string proxy)
    {
        var configure = () => CreateServices(proxy);
        configure.Should().Throw<InvalidOperationException>().WithMessage("*specific proxy IP*");
    }

    [Test]
    public async Task ForwardedScheme_NoHttpsRedirectPort_DirectHttpRemainsUsable()
    {
        using var services = CreateServices("192.0.2.10", httpsPort: null);
        var context = CreateContext(services, "192.0.2.11", null);

        await CreatePipeline(services, redirect: true)(context);

        context.Response.StatusCode.Should().Be(200);
        context.Request.Scheme.Should().Be("http");
        context.Response.Headers.Location.Should().BeEmpty();
        context.Response.Headers.SetCookie.ToString().Should().Contain("XFramework.Portal=").And.NotContain("; secure");
    }

    [Test]
    public void ForwardedScheme_Configuration_RetainsFrameworkTrustDefaultsAndLimitsHeaders()
    {
        using var services = CreateServices("192.0.2.10");
        var options = services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedProto);
        options.ForwardLimit.Should().Be(1);
        options.KnownProxies.Should().Contain(IPAddress.Parse("192.0.2.10"));
        options.KnownIPNetworks.Should().HaveCount(new ForwardedHeadersOptions().KnownIPNetworks.Count);
        options.KnownIPNetworks.Single().Contains(IPAddress.Parse("127.1.2.3")).Should().BeTrue();
        options.KnownIPNetworks.Single().Contains(IPAddress.Parse("128.0.0.1")).Should().BeFalse();
        options.KnownIPNetworks.Single().Contains(IPAddress.Parse("192.0.2.11")).Should().BeFalse();
    }

    [Test]
    public void Program_Forwarding_PrecedesSchemeSensitiveMiddlewareAndRetainsCookiePolicy()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src/Presentation/XFramework.Portal/Program.cs"));
        source.Should().Contain("builder.Services.AddPortalForwardedScheme(builder.Configuration);");
        source.Should().Contain("options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;");
        var forwarding = source.IndexOf("app.UseForwardedHeaders();", StringComparison.Ordinal);
        forwarding.Should().BeGreaterThan(0);
        foreach (var middleware in new[] { "app.UseHsts();", "app.UseHttpsRedirection();", "app.UseAntiforgery();", "app.UseAuthentication();" })
            source.IndexOf(middleware, StringComparison.Ordinal).Should().BeGreaterThan(forwarding);
    }

    private static ServiceProvider CreateServices(string? proxy, int? httpsPort = 8443)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Portal:TrustedProxyIp"] = proxy }).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddPortalForwardedScheme(configuration);
        services.AddHttpsRedirection(options => options.HttpsPort = httpsPort);
        services.AddAuthentication("portal").AddCookie("portal", options =>
        {
            options.Cookie.Name = "XFramework.Portal";
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services, string peer, string? proto)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("portal.test", 5000);
        context.Request.Path = "/";
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        if (proto is not null)
            context.Request.Headers["X-Forwarded-Proto"] = proto;
        return context;
    }

    private static RequestDelegate CreatePipeline(IServiceProvider services, bool redirect = false)
    {
        var app = new ApplicationBuilder(services);
        app.ServerFeatures.Set<IServerAddressesFeature>(new ServerAddressesFeature());
        app.UseForwardedHeaders();
        if (redirect)
            app.UseHttpsRedirection();
        app.UseAuthentication();
        app.Run(context => context.SignInAsync("portal", new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "scheme-test")], "portal"))));
        return app.Build();
    }

    private static string RepositoryRoot([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "../../.."));
}
