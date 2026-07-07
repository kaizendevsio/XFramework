using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Bolt.Client;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(30000)]
public sealed class BoltAuthorizedHandshakeTests
{
    private const string TestScheme = "BoltHandshakeTest";
    private static int _portCounter = 20700;
    private WebApplication _app = null!;
    private int _port;

    [SetUp]
    public async Task SetUp()
    {
        _port = Interlocked.Increment(ref _portCounter);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_port}");
        builder.Services.AddBoltServer();
        builder.Services
            .AddAuthentication(TestScheme)
            .AddScheme<AuthenticationSchemeOptions, BoltHandshakeTestAuthHandler>(TestScheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseWebSockets();
        _app.MapBolt("/bolt/ws").RequireAuthorization();
        _app.MapBolt("/other/ws").RequireAuthorization();
        _app.MapGet("/health", () => Results.Ok("ok"));

        _ = Task.Run(() => _app.RunAsync());
        await WaitForHealth($"http://localhost:{_port}/health");
    }

    [TearDown]
    public async Task TearDown()
    {
        try { await _app.StopAsync(); } catch { }
        try { await _app.DisposeAsync(); } catch { }
    }

    [Test]
    public async Task BoltHandshake_QueryTokenOnProductionPath_Connects()
    {
        await using var client = new BoltClient(
            new Uri($"ws://localhost:{_port}/bolt/ws"),
            "auth-client",
            "AuthClient",
            new BoltClientOptions
            {
                AccessToken = "valid-query-token",
                SendAccessTokenAsQueryString = true,
                RpcTimeoutSeconds = 5
            },
            NullLogger<BoltClient>.Instance);

        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();
    }

    [Test]
    public async Task BoltHandshake_MissingToken_IsRejected()
    {
        using var socket = new ClientWebSocket();

        var connect = async () => await socket.ConnectAsync(
            new Uri($"ws://localhost:{_port}/bolt/ws"),
            CancellationToken.None);

        await connect.Should().ThrowAsync<WebSocketException>();
    }

    [Test]
    public async Task BoltHandshake_QueryTokenOnNonBoltWsPath_IsRejected()
    {
        using var socket = new ClientWebSocket();

        var connect = async () => await socket.ConnectAsync(
            new Uri($"ws://localhost:{_port}/other/ws?access_token=valid-query-token"),
            CancellationToken.None);

        await connect.Should().ThrowAsync<WebSocketException>();
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
                    return;
            }
            catch
            {
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }

    private sealed class BoltHandshakeTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? token = null;

            if (Request.Path.StartsWithSegments("/bolt/ws") &&
                Request.Query.TryGetValue("access_token", out var queryToken))
            {
                token = queryToken.ToString();
            }

            if (!string.Equals(token, "valid-query-token", StringComparison.Ordinal))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "auth-client")], Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
