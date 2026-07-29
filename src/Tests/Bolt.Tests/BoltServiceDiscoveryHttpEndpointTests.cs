using Bolt.Hub.Extensions;
using Bolt.Hub.Configurations;
using Bolt.Hub.Security;
using Bolt.Hub.Services;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.ServiceIdentity;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(30000)]
public sealed class BoltServiceDiscoveryHttpEndpointTests
{
    private static int _portCounter = 20300;
    private WebApplication _app = null!;
    private int _port;

    [SetUp]
    public async Task SetUp()
    {
        _port = Interlocked.Increment(ref _portCounter);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_port}");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltServiceDiscovery:ExposeHttpEndpoints"] = "true"
        });

        builder.Services.AddDbContext<DbContext, AppDbContext>(options =>
            options.UseInMemoryDatabase($"bolt-discovery-http-{Guid.NewGuid():N}"));
        builder.Services.AddBoltServer();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<IBoltServicePresenceTracker, BoltServicePresenceTracker>();
        builder.Services.AddScoped<IBoltServiceDiscoveryRegistry, BoltServiceDiscoveryRegistry>();
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, BoltDiscoveryTestAuthHandler>(
                JwtBearerDefaults.AuthenticationScheme,
                _ => { })
            .AddScheme<AuthenticationSchemeOptions, BoltDiscoveryTestAuthHandler>(
                BoltTransportAuthentication.Scheme,
                _ => { });
        builder.Services.AddAuthorization(BoltAuthorizationPolicies.AddServiceDiscoveryReaderPolicy);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();
        _app.UseAppServices();
        _app.MapGet("/health", () => Results.Ok("ok"));

        _ = Task.Run(() => _app.RunAsync());
        await WaitForHealth($"http://localhost:{_port}/health");
    }

    [TearDown]
    public async Task TearDown()
    {
        try { await _app.StopAsync(); } catch { }
        try { await _app.DisposeAsync(); } catch { }
        try { await _app.DisposeAsync(); } catch { }
    }

    [TestCase("/api/bolt/services")]
    [TestCase("/api/bolt/modules")]
    public async Task DiscoveryEndpoint_AnonymousRequest_IsRejected(string path)
    {
        using var client = new HttpClient();

        var response = await client.GetAsync($"http://localhost:{_port}{path}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestCase("/api/bolt/services")]
    [TestCase("/api/bolt/modules")]
    public async Task DiscoveryEndpoint_AuthenticatedUserWithoutServiceOrAdminScope_IsForbidden(string path)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "user");

        var response = await client.GetAsync($"http://localhost:{_port}{path}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestCase("/api/bolt/services")]
    [TestCase("/api/bolt/modules")]
    public async Task DiscoveryEndpoint_ServiceScope_IsAllowed(string path)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "service");

        var response = await client.GetAsync($"http://localhost:{_port}{path}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestCase("/api/bolt/services")]
    [TestCase("/api/bolt/modules")]
    public async Task DiscoveryEndpoint_ServiceScopeInScpClaim_IsAllowed(string path)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "service-scp");

        var response = await client.GetAsync($"http://localhost:{_port}{path}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestCase("/api/bolt/services")]
    [TestCase("/api/bolt/modules")]
    public async Task DiscoveryEndpoint_AdminScope_IsAllowed(string path)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin-scope");

        var response = await client.GetAsync($"http://localhost:{_port}{path}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestCase("/api/bolt/services")]
    [TestCase("/api/bolt/modules")]
    public async Task DiscoveryEndpoint_AdminRole_IsAllowed(string path)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin-role");

        var response = await client.GetAsync($"http://localhost:{_port}{path}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await client.GetAsync(url)).IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }

    private sealed class BoltDiscoveryTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authorization = Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var token = authorization["Bearer ".Length..].Trim();
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, token)
            };

            switch (token)
            {
                case "service":
                    claims.Add(new("scope", XFrameworkServiceScopes.BoltService));
                    break;
                case "service-scp":
                    claims.Add(new("scp", $"profile {XFrameworkServiceScopes.BoltService}"));
                    break;
                case "admin-scope":
                    claims.Add(new("scope", XFrameworkServiceScopes.IdentityAdmin));
                    break;
                case "admin-role":
                    claims.Add(new(ClaimTypes.Role, "Admin"));
                    break;
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
