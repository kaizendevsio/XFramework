using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltClientTransportReliabilityTests
{
    [Test]
    public async Task PushAsync_TransportWriteBlocked_DoesNotCompleteAtQueueAdmission()
    {
        var transport = new BlockingBoltConnection();
        var connection = new BoltConnection(transport, sendQueueCapacity: 4, sendEnqueueTimeoutMs: 1000);
        using var sendCts = new CancellationTokenSource();
        connection.StartSendLoop(sendCts.Token);
        await using var client = CreateAttachedClient(connection);

        var push = client.PushAsync("receiver", "update", new byte[] { 1, 2, 3 }).AsTask();

        await transport.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        push.IsCompleted.Should().BeFalse();

        transport.Release();
        await push.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task InvokeAsync_TransportWriteFails_FailsPromptlyAndRetiresConnection()
    {
        var transport = new ThrowingBoltConnection();
        var connection = new BoltConnection(transport, sendQueueCapacity: 4, sendEnqueueTimeoutMs: 1000);
        using var sendCts = new CancellationTokenSource();
        connection.StartSendLoop(sendCts.Token);
        var client = CreateAttachedClient(connection);
        SetField(client, "_disposed", true); // Suppress reconnect for this isolated transport test.

        Func<Task> invoke = async () =>
            await client.InvokeAsync("receiver", "update", new byte[] { 1, 2, 3 });

        await invoke.Should().ThrowAsync<IOException>()
            .WithMessage("transport send failed");
        await WaitUntilAsync(() => client.GetHealthSnapshot().ConnectionCount == 0);

        var snapshot = client.GetHealthSnapshot();
        snapshot.TotalSendFailures.Should().Be(1);
        snapshot.IsHealthy.Should().BeFalse();

        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(2));
        await transport.DisposeAsync();
    }

    [Test]
    public async Task InvokeAsync_CanceledDuringTransportWrite_KeepsConnectionAndQueuesRequestCancel()
    {
        var transport = new BlockingBoltConnection();
        var connection = new BoltConnection(transport, sendQueueCapacity: 4, sendEnqueueTimeoutMs: 5000);
        using var sendLoopCts = new CancellationTokenSource();
        connection.StartSendLoop(sendLoopCts.Token);
        var client = CreateAttachedClient(connection);
        SetField(client, "_disposed", true); // Suppress reconnect for this isolated transport test.
        using var callerCts = new CancellationTokenSource();

        var invocation = client.InvokeAsync(
            "receiver",
            "update",
            new byte[] { 1, 2, 3 },
            callerCts.Token);
        await transport.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        callerCts.Cancel();

        await FluentActions.Awaiting(async () => await invocation)
            .Should().ThrowAsync<OperationCanceledException>();
        client.GetHealthSnapshot().ConnectionCount.Should().Be(1);

        transport.Release();
        await WaitUntilAsync(() => connection.PendingSends == 0);
        transport.SentFrameTypes.Should().ContainInOrder(FrameType.Request, FrameType.RequestCancel);
        transport.IsConnected.Should().BeTrue();
        connection.SendLoop!.IsCompleted.Should().BeFalse();

        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(2));
        await transport.DisposeAsync();
    }

    private static BoltClient CreateAttachedClient(BoltConnection connection)
    {
        var client = new BoltClient(
            new Uri("ws://localhost:1/bolt"),
            "reliable_sender",
            "ReliableSender",
            new BoltClientOptions { RpcTimeoutSeconds = 30 },
            NullLogger<BoltClient>.Instance);

        var connections = (List<BoltConnection>)GetField(client, "_connections");
        connections.Add(connection);
        SetField(client, "_isRegistered", true);
        typeof(BoltClient)
            .GetMethod("ObserveConnection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(client, [connection]);
        return client;
    }

    private static object GetField(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        condition().Should().BeTrue();
    }

    private class TestBoltConnection : IBoltConnection
    {
        public bool SupportsDatagrams => false;
        public bool IsConnected { get; protected set; } = true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public virtual ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default) => ValueTask.FromResult((0, true));

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default)
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingBoltConnection : TestBoltConnection
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<FrameType> SentFrameTypes { get; } = new();

        public override ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            SentFrameTypes.Enqueue((FrameType)data.Span[0]);
            SendStarted.TrySetResult();
            return new ValueTask(_release.Task);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class ThrowingBoltConnection : TestBoltConnection
    {
        public override ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.FromException(new IOException("transport send failed"));
    }
}
