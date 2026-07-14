using System.Net;
using System.Reflection;
using System.Collections.Concurrent;
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
        builder.Services.AddBoltServer(options =>
        {
            options.InvocationTimeoutMs = 250;
            options.CleanupIntervalSeconds = 1;
            options.MaxPendingRpcCalls = 1;
            options.MaxPendingRpcCallsPerPrincipal = 1;
        });
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
    public async Task InvokeAsync_WhenResponderNeverReplies_ReturnsHubGatewayTimeout()
    {
        var responder = CreateClient("timeout_responder", "TimeoutResponder");
        var caller = CreateClient("timeout_caller", "TimeoutCaller");
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        responder.RegisterHandler("hang", async (_, _, ct) =>
        {
            handlerEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return (HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty);
        });

        await responder.ConnectAsync();
        await caller.ConnectAsync();

        var started = DateTime.UtcNow;
        var invokeTask = caller.InvokeAsync("timeout_responder", "hang", new byte[] { 1 });
        await handlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var (statusCode, responsePayload) = await invokeTask.WaitAsync(TimeSpan.FromSeconds(5));

        statusCode.Should().Be(HttpStatusCode.GatewayTimeout);
        responsePayload.Length.Should().Be(0);
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(4));
        GetServerPendingInvocationCount().Should().Be(0);

        var secondInvoke = caller.InvokeAsync("timeout_responder", "hang", new byte[] { 2 });
        var (secondStatusCode, secondResponsePayload) = await secondInvoke.WaitAsync(TimeSpan.FromSeconds(5));

        secondStatusCode.Should().Be(HttpStatusCode.GatewayTimeout,
            "the first timeout must release global and per-principal pending-invocation capacity");
        secondResponsePayload.Length.Should().Be(0);
        GetServerPendingInvocationCount().Should().Be(0);

        await caller.DisposeAsync();
        await responder.DisposeAsync();
    }

    [Test]
    public async Task InvokeAsync_WhenResponderDisconnectsDuringPendingInvocation_ReturnsServiceUnavailable()
    {
        var responder = CreateClient("disconnect_responder", "DisconnectResponder");
        var caller = CreateClient("disconnect_caller", "DisconnectCaller");
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        responder.RegisterHandler("hang", async (_, _, ct) =>
        {
            handlerEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return (HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty);
        });

        await responder.ConnectAsync();
        await caller.ConnectAsync();

        var invokeTask = caller.InvokeAsync("disconnect_responder", "hang", new byte[] { 1 });
        await handlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await responder.DisposeAsync();

        var (statusCode, responsePayload) = await invokeTask.WaitAsync(TimeSpan.FromSeconds(5));

        statusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        responsePayload.Length.Should().Be(0);
        GetServerPendingInvocationCount().Should().Be(0);

        await caller.DisposeAsync();
    }

    [Test]
    public async Task BoltHubConnection_SendAsync_WhenQueueFull_ObservesEnqueueTimeoutAndReturnsFailedBuffer()
    {
        var connection = new BoltHubConnection(new NoopBoltConnection(), sendQueueCapacity: 1, sendEnqueueTimeoutMs: 25);

        await connection.SendAsync(new byte[10], CancellationToken.None);

        Func<Task> enqueueWhenFull = async () => await connection.SendAsync(new byte[20], CancellationToken.None).AsTask();

        await enqueueWhenFull.Should().ThrowAsync<BoltSendEnqueueTimeoutException>()
            .WithMessage("*enqueue timed out*");
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
    public async Task GetHealthSnapshot_AfterConnectionRemoval_RetainsPhaseZeroFailureWatermarks()
    {
        await using var client = new BoltClient(
            new Uri($"ws://localhost:{_port}/bolt"),
            "watermark_caller",
            "WatermarkCaller",
            new BoltClientOptions { SendEnqueueTimeoutMs = 25 },
            _loggerFactory.CreateLogger<BoltClient>());

        var enqueueTimeoutConnection = new BoltConnection(
            new NoopBoltConnection(),
            sendQueueCapacity: 1,
            sendEnqueueTimeoutMs: 25);
        ObserveClientConnection(client, enqueueTimeoutConnection);
        await enqueueTimeoutConnection.SendAsync(new byte[] { 1 }, CancellationToken.None);
        Func<Task> enqueueTimeout = async () =>
            await enqueueTimeoutConnection.SendAsync(new byte[] { 2 }, CancellationToken.None);
        await enqueueTimeout.Should().ThrowAsync<OperationCanceledException>();
        enqueueTimeoutConnection.StartSendLoop(CancellationToken.None);
        enqueueTimeoutConnection.CompleteSendChannel();
        await enqueueTimeoutConnection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(2));

        var sendTimeoutConnection = new BoltConnection(
            new CancellationAwareBlockingSendBoltConnection(),
            sendQueueCapacity: 1,
            sendEnqueueTimeoutMs: 25);
        ObserveClientConnection(client, sendTimeoutConnection);
        using var sendTimeoutCts = new CancellationTokenSource();
        sendTimeoutConnection.StartSendLoop(sendTimeoutCts.Token);
        await sendTimeoutConnection.SendAsync(new byte[] { 3 }, CancellationToken.None);
        await WaitForConditionAsync(() => client.GetHealthSnapshot().TotalSendTimeouts == 2, 2000);
        sendTimeoutCts.Cancel();
        sendTimeoutConnection.CompleteSendChannel();
        await sendTimeoutConnection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(2));

        var sendFailureConnection = new BoltConnection(new ThrowingSendBoltConnection());
        ObserveClientConnection(client, sendFailureConnection);
        using var sendFailureCts = new CancellationTokenSource();
        sendFailureConnection.StartSendLoop(sendFailureCts.Token);
        await sendFailureConnection.SendAsync(new byte[] { 4 }, CancellationToken.None);
        await WaitForConditionAsync(() => client.GetHealthSnapshot().TotalSendFailures == 1);
        sendFailureConnection.CompleteSendChannel();
        await sendFailureConnection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(2));

        var enqueueFailureConnection = new BoltConnection(new NoopBoltConnection());
        ObserveClientConnection(client, enqueueFailureConnection);
        enqueueFailureConnection.CompleteSendChannel();
        Func<Task> enqueueFailure = async () =>
            await enqueueFailureConnection.SendAsync(new byte[] { 5 }, CancellationToken.None);
        await enqueueFailure.Should().ThrowAsync<InvalidOperationException>();

        var loopConnection = new BoltConnection(new NoopBoltConnection());
        ObserveClientConnection(client, loopConnection);
        typeof(BoltConnection)
            .GetMethod("RecordReceiveLoopFault", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(loopConnection, null);
        typeof(BoltConnection)
            .GetMethod("RecordUnexpectedDisconnect", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(loopConnection, null);

        var snapshot = client.GetHealthSnapshot();
        snapshot.ConnectionCount.Should().Be(0);
        snapshot.TotalSendFailures.Should().Be(2);
        snapshot.TotalSendTimeouts.Should().Be(2);
        snapshot.TotalReceiveLoopFaults.Should().Be(1);
        snapshot.TotalUnexpectedDisconnects.Should().Be(1);
        snapshot.TotalSuccessfulReconnects.Should().Be(0);
    }

    [Test]
    public async Task GetHealthSnapshot_AfterSuccessfulReconnect_RetainsReconnectWatermark()
    {
        await using var client = CreateClient("reconnect_watermark", "ReconnectWatermark");
        var reconnect = typeof(BoltClient)
            .GetMethod("ReconnectAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)reconnect.Invoke(client, null)!;

        var snapshot = client.GetHealthSnapshot();
        snapshot.IsHealthy.Should().BeTrue();
        snapshot.TotalSuccessfulReconnects.Should().Be(1);
        snapshot.TotalSendFailures.Should().Be(0);
        snapshot.TotalSendTimeouts.Should().Be(0);
        snapshot.TotalReceiveLoopFaults.Should().Be(0);
        snapshot.TotalUnexpectedDisconnects.Should().Be(0);
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
    public async Task InvokeAsync_WhenResponseNeverArrives_HonorsCallerCancellation()
    {
        await using var client = new BoltClient(
            new Uri($"ws://localhost:{_port}/bolt"),
            "cancel_wait_caller",
            "CancelWaitCaller",
            new BoltClientOptions { RpcTimeoutSeconds = 30, SendQueueCapacity = 4 },
            _loggerFactory.CreateLogger<BoltClient>());
        var connection = new BoltConnection(new NoopBoltConnection(), sendQueueCapacity: 4);

        var connections = (List<BoltConnection>)typeof(BoltClient)
            .GetField("_connections", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        connections.Add(connection);
        typeof(BoltClient)
            .GetField("_isRegistered", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, true);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(75));
        var started = DateTime.UtcNow;
        Func<Task> invoke = async () => await client.InvokeAsync(
            "missing_receiver",
            "noop",
            new byte[] { 1 },
            cts.Token);

        await invoke.Should().ThrowAsync<OperationCanceledException>();
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(2));

        var pendingCalls = (ConcurrentDictionary<Guid, PooledRpcCall>)typeof(BoltClient)
            .GetField("_pendingCalls", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        pendingCalls.Should().BeEmpty();

        connection.CompleteSendChannel();
    }

    [Test]
    public async Task InvokeAsync_WhenResponseNeverArrives_UsesContextualRpcTimeoutAndClearsPendingCall()
    {
        await using var client = new BoltClient(
            new Uri($"ws://localhost:{_port}/bolt"),
            "timeout_wait_caller",
            "TimeoutWaitCaller",
            new BoltClientOptions { RpcTimeoutSeconds = 1, SendQueueCapacity = 4 },
            _loggerFactory.CreateLogger<BoltClient>());
        var connection = new BoltConnection(new NoopBoltConnection(), sendQueueCapacity: 4);

        var connections = (List<BoltConnection>)typeof(BoltClient)
            .GetField("_connections", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        connections.Add(connection);
        typeof(BoltClient)
            .GetField("_isRegistered", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, true);

        Func<Task> invoke = async () => await client.InvokeAsync(
            "missing_receiver",
            "noop",
            new byte[] { 1 });

        await invoke.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*timed out before the request completed*");

        var pendingCalls = (ConcurrentDictionary<Guid, PooledRpcCall>)typeof(BoltClient)
            .GetField("_pendingCalls", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        pendingCalls.Should().BeEmpty();

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

    private static void ObserveClientConnection(BoltClient client, BoltConnection connection) =>
        typeof(BoltClient)
            .GetMethod("ObserveConnection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(client, [connection]);

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

    private int GetServerPendingInvocationCount()
    {
        var server = _serverApp.Services.GetRequiredService<BoltServer>();
        var field = typeof(BoltServer).GetField("_pendingInvocations", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BoltServer pending-invocation field not found.");
        var pendingInvocations = field.GetValue(server)!;
        var countProperty = pendingInvocations.GetType().GetProperty("Count")
            ?? throw new InvalidOperationException("BoltServer pending-invocation collection does not expose Count.");
        return (int)countProperty.GetValue(pendingInvocations)!;
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

    private sealed class CancellationAwareBlockingSendBoltConnection : NoopBoltConnection
    {
        public override ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            new(Task.Delay(Timeout.InfiniteTimeSpan, ct));
    }

    private sealed class ThrowingSendBoltConnection : NoopBoltConnection
    {
        public override ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.FromException(new IOException("transport send failed"));
    }
}
