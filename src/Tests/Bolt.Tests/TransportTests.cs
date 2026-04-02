using System.Net.WebSockets;
using Bolt.Client;
using Bolt.Client.Transport;
using Bolt.Protocol.Transport;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class WebSocketBoltConnectionTests
{
    [Test]
    public void TransportType_IsWebSocket()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        conn.TransportType.Should().Be(BoltTransport.WebSocket);
    }

    [Test]
    public void SupportsDatagrams_IsFalse()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        conn.SupportsDatagrams.Should().BeFalse();
    }

    [Test]
    public async Task SendDatagramAsync_IsNoOp()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        await conn.SendDatagramAsync(new byte[] { 1, 2, 3 });
    }
}

[TestFixture]
public class WebSocketTransportConnectTests
{
    private WebApplication _hubApp = null!;
    private const int WsPort = 18700;

    [OneTimeSetUp]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{WsPort}");
        builder.Services.AddBoltServer();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _hubApp = builder.Build();
        _hubApp.UseWebSockets();
        _hubApp.MapBolt("/bolt");
        _hubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _hubApp.RunAsync());
        await WaitForHealth($"http://localhost:{WsPort}/health");
    }

    [Test]
    public async Task WebSocket_ConnectsSuccessfully()
    {
        var lf = _hubApp.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions
        {
            RpcTimeoutSeconds = 10,
            TransportAttemptTimeoutMs = 3000,
            PreferredTransports = [BoltTransport.WebSocket]
        };

        var client = new BoltClient(
            new Uri($"ws://localhost:{WsPort}/bolt"),
            "ws_connect_client", "WsConnectClient", opts, lf.CreateLogger<BoltClient>());
        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();

        await client.DisposeAsync();
    }

    [OneTimeTearDown]
    public async Task Cleanup()
    {
        try { if (_hubApp != null) await _hubApp.StopAsync(); } catch { }
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await client.GetAsync(url)).IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(100);
        }
        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }
}
