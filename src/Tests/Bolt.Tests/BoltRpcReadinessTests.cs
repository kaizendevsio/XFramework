using System.Net;
using Bolt.Client;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(30000)]
public class BoltRpcReadinessTests
{
    private WebApplication _serverApp = null!;
    private ILoggerFactory _loggerFactory = null!;
    private static int _portCounter = 19700;
    private int _port;

    [SetUp]
    public async Task SetUp()
    {
        _port = Interlocked.Increment(ref _portCounter);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_port}");
        builder.Services.AddBoltServer();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _serverApp = builder.Build();
        _serverApp.UseWebSockets();
        _serverApp.MapBolt("/bolt");
        _serverApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _serverApp.RunAsync());
        await WaitForHealth($"http://localhost:{_port}/health");
        _loggerFactory = _serverApp.Services.GetRequiredService<ILoggerFactory>();
    }

    [TearDown]
    public async Task TearDown()
    {
        try { await _serverApp.StopAsync(); } catch { }
        try { await _serverApp.DisposeAsync(); } catch { }
    }

    [Test]
    public async Task PooledRpcCall_TimeoutThenLateResponse_IgnoresLateCompletion()
    {
        using var cts = new CancellationTokenSource();
        var call = PooledRpcCall.Rent();
        var task = call.GetTask().AsTask();

        call.RegisterTimeout(cts.Token);
        cts.Cancel();

        Action lateResponse = () => call.SetResult(new BoltRpcResponse
        {
            StatusCode = HttpStatusCode.OK,
            Data = ReadOnlyMemory<byte>.Empty
        });

        lateResponse.Should().NotThrow();
        Func<Task> observeTimeout = async () => await task;
        await observeTimeout.Should().ThrowAsync<TimeoutException>();
    }

    [Test]
    public async Task InvokeAsync_WhenDisconnected_DoesNotReplayAfterConnect()
    {
        var receiver = CreateClient("offline_receiver", "OfflineReceiver");
        var caller = CreateClient("offline_caller", "OfflineCaller");
        var handled = 0;

        receiver.RegisterHandler("mutate", (_, _) =>
        {
            Interlocked.Increment(ref handled);
            return Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty));
        });

        await receiver.ConnectAsync();

        Func<Task> disconnectedCall = async () =>
            await caller.InvokeAsync("offline_receiver", "mutate", new byte[] { 1, 2, 3 });

        await disconnectedCall.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Not connected");

        await caller.ConnectAsync();
        await Task.Delay(300);

        handled.Should().Be(0, "a failed disconnected RPC must not be replayed later without its caller");

        await caller.DisposeAsync();
        await receiver.DisposeAsync();
    }

    private BoltClient CreateClient(string id, string name) =>
        new(new Uri($"ws://localhost:{_port}/bolt"), id, name,
            new BoltClientOptions { RpcTimeoutSeconds = 5 }, _loggerFactory.CreateLogger<BoltClient>());

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
