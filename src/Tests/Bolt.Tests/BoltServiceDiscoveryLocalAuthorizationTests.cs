using System.Net;
using System.Security.Claims;
using Bolt.Client;
using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using Bolt.Hub.Security;
using Bolt.Hub.Services;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using XFramework.Domain.Shared.ServiceIdentity;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(30000)]
public sealed class BoltServiceDiscoveryLocalAuthorizationTests
{
    private static int _portCounter = 20400;
    private WebApplication _app = null!;
    private IBoltServiceDiscoveryRegistry _registry = null!;
    private ILoggerFactory _loggerFactory = null!;
    private int _port;

    [SetUp]
    public async Task SetUp()
    {
        _port = Interlocked.Increment(ref _portCounter);
        _registry = Substitute.For<IBoltServiceDiscoveryRegistry>();
        _registry.ResetPresenceAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _registry.MarkConnectedAsync(Arg.Any<BoltClientConnectionEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _registry.MarkDisconnectedAsync(Arg.Any<BoltClientConnectionEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_port}");
        builder.Services.AddBoltServer(options =>
            options.RegistrationIdentityBindingMode = BoltRegistrationIdentityBindingMode.Enforce);
        builder.Services.AddScoped(_ => _registry);
        builder.Services.AddHostedService<BoltServiceDiscoveryHostedService>();
        builder.Services.AddAuthorization(BoltAuthorizationPolicies.AddServiceDiscoveryReaderPolicy);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();
        _app.UseWebSockets();
        _app.Use(async (context, next) =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authorization["Bearer ".Length..].Trim();
                context.User = token.StartsWith("service:", StringComparison.Ordinal)
                    ? CreateServicePrincipal(token["service:".Length..])
                    : CreateUserPrincipal(token);
            }

            await next();
        });
        _app.MapBolt("/bolt");
        _app.MapGet("/health", () => "ok");
        _ = Task.Run(() => _app.RunAsync());
        await WaitForHealth($"http://localhost:{_port}/health");
        _loggerFactory = _app.Services.GetRequiredService<ILoggerFactory>();
    }

    [TearDown]
    public async Task TearDown()
    {
        try { await _app.StopAsync(); } catch { }
        try { await _app.DisposeAsync(); } catch { }
    }

    [TestCase(BoltServiceDiscoveryCommands.GetServiceRegistry)]
    [TestCase(BoltServiceDiscoveryCommands.GetModuleRegistry)]
    public async Task RegistryRead_UserWithoutPolicy_IsForbiddenBeforeParsingOrRegistryWork(string command)
    {
        await using var client = CreateClient("normal-user", "NormalUser", "normal-user");
        await client.ConnectAsync();

        var action = async () => await client.InvokeAsync(string.Empty, command, new byte[] { 0xFF, 0xFF });

        var result = (await action.Should().NotThrowAsync()).Which;
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _ = _registry.DidNotReceive().GetServicesAsync(
            Arg.Any<BoltServiceRegistryRequest>(),
            Arg.Any<CancellationToken>());
        _ = _registry.DidNotReceive().GetModulesAsync(
            Arg.Any<BoltModuleRegistryRequest>(),
            Arg.Any<CancellationToken>());
    }

    private BoltClient CreateClient(string id, string name, string token) =>
        new(
            new Uri($"ws://localhost:{_port}/bolt"),
            id,
            name,
            new BoltClientOptions { RpcTimeoutSeconds = 5, AccessToken = token },
            _loggerFactory.CreateLogger<BoltClient>());

    private static ClaimsPrincipal CreateServicePrincipal(string serviceName) =>
        new(new ClaimsIdentity(
            [
                new("client_id", serviceName),
                new("service", serviceName),
                new("scope", XFrameworkServiceScopes.BoltService),
                new(ClaimTypes.Name, serviceName)
            ],
            "TestService"));

    private static ClaimsPrincipal CreateUserPrincipal(string name) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, name)],
            "TestUser"));

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static async Task WaitForHealth(string url)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await client.GetAsync(url)).IsSuccessStatusCode)
                    return;
            }
            catch
            {
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Service at {url} did not become healthy.");
    }
}
