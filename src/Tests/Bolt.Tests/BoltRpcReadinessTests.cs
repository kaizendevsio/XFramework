using System.Net;
using System.Reflection;
using Bolt.Client;
using Bolt.Protocol.Transport;
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

    [Test]
    public async Task BoltHubConnection_SendAsync_WhenQueueFull_ObservesEnqueueTimeoutAndReturnsFailedBuffer()
    {
        var connection = new BoltHubConnection(new NoopBoltConnection(), sendQueueCapacity: 1, sendEnqueueTimeoutMs: 25);

        await connection.SendAsync(new byte[10], CancellationToken.None);

        Func<Task> enqueueWhenFull = async () => await connection.SendAsync(new byte[20], CancellationToken.None).AsTask();

        await enqueueWhenFull.Should().ThrowAsync<OperationCanceledException>();
        connection.PendingBytes.Should().Be(10);
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task BoltHubConnection_SendLoopCancellation_DrainsQueuedBuffersAndClearsPendingBytes()
    {
        var transport = new BlockingSendBoltConnection();
        var connection = new BoltHubConnection(transport, sendQueueCapacity: 4, sendEnqueueTimeoutMs: 25);
        using var receiveCts = new CancellationTokenSource();
        connection.StartSendLoop(receiveCts.Token);

        await connection.SendAsync(new byte[10], CancellationToken.None);
        await WaitForConditionAsync(() => connection.PendingBytes == 10);
        await connection.SendAsync(new byte[20], CancellationToken.None);
        connection.PendingBytes.Should().Be(30);

        receiveCts.Cancel();
        transport.Release();
        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(2));

        connection.PendingBytes.Should().Be(0);
    }

    [Test]
    public async Task BoltConnection_SendAsync_WhenQueueFull_ObservesEnqueueTimeout()
    {
        var connection = new BoltConnection(new NoopBoltConnection(), sendQueueCapacity: 1, sendEnqueueTimeoutMs: 25);

        await connection.SendAsync(new byte[] { 1 }, CancellationToken.None);

        Func<Task> enqueueWhenFull = async () => await connection.SendAsync(new byte[] { 2 }, CancellationToken.None).AsTask();

        await enqueueWhenFull.Should().ThrowAsync<OperationCanceledException>();
        connection.PendingSends.Should().Be(1);
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task InvokeAsync_WhenSendQueueFull_TimesOutAtRpcTimeout()
    {
        var options = new BoltClientOptions
        {
            RpcTimeoutSeconds = 1,
            SendQueueCapacity = 1,
            SendEnqueueTimeoutMs = 0
        };
        await using var client = new BoltClient(
            new Uri($"ws://localhost:{_port}/bolt"),
            "blocked_caller",
            "BlockedCaller",
            options,
            _loggerFactory.CreateLogger<BoltClient>());
        var connection = new BoltConnection(new NoopBoltConnection(), sendQueueCapacity: 1);
        await connection.SendAsync(new byte[] { 1 }, CancellationToken.None);

        var connections = (List<BoltConnection>)typeof(BoltClient)
            .GetField("_connections", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        connections.Add(connection);
        typeof(BoltClient)
            .GetField("_isRegistered", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, true);

        var started = DateTime.UtcNow;
        Func<Task> invoke = async () => await client.InvokeAsync("blocked_receiver", "noop", new byte[] { 2 });

        await invoke.Should().ThrowAsync<TimeoutException>();
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(5));
        connection.PendingSends.Should().Be(1);
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task GetHealthSnapshot_WhenPendingSendsExceedThreshold_IsUnhealthy()
    {
        await using var client = new BoltClient(
            new Uri($"ws://localhost:{_port}/bolt"),
            "health_caller",
            "HealthCaller",
            new BoltClientOptions { SendEnqueueTimeoutMs = 25 },
            _loggerFactory.CreateLogger<BoltClient>());
        var transport = new BlockingSendBoltConnection();
        var connection = new BoltConnection(transport, sendQueueCapacity: 4, sendEnqueueTimeoutMs: 25);
        var receiveCts = new CancellationTokenSource();
        connection.ReceiveCts = receiveCts;
        connection.ReceiveLoop = Task.Delay(Timeout.InfiniteTimeSpan, receiveCts.Token);
        connection.StartSendLoop(receiveCts.Token);

        await connection.SendAsync(new byte[] { 1 }, CancellationToken.None);

        var connections = (List<BoltConnection>)typeof(BoltClient)
            .GetField("_connections", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        connections.Add(connection);
        typeof(BoltClient)
            .GetField("_isRegistered", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, true);

        await WaitForConditionAsync(() => connection.ActiveSends > 0);
        await WaitForConditionAsync(() => connection.ActiveSendElapsedMs > 25, timeoutMs: 2000);
        var snapshot = client.GetHealthSnapshot();

        snapshot.ActiveSends.Should().Be(1);
        snapshot.MaxActiveSendElapsedMs.Should().BeGreaterThan(snapshot.ActiveSendUnhealthyThresholdMs);
        snapshot.IsHealthy.Should().BeFalse();

        transport.Release();
        receiveCts.Cancel();
        connection.CompleteSendChannel();
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

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 1000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met before timeout.");
    }

    private class NoopBoltConnection : IBoltConnection
    {
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public virtual ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default) =>
            ValueTask.FromResult((0, true));

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingSendBoltConnection : NoopBoltConnection
    {
        private readonly TaskCompletionSource _sendGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            new(_sendGate.Task);

        public void Release() => _sendGate.TrySetResult();
    }
}
