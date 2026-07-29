using System.Reflection;
using Bolt.Protocol.Transport;
using Bolt.Server;
using FluentAssertions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(15000)]
public sealed class BoltHubSendOptimizationTests
{
    [Test]
    public async Task CapacitySignal_MultipleWaiters_WakesWithoutLosingByteAccounting()
    {
        await using var transport = new SynchronousSendConnection();
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 32,
            sendEnqueueTimeoutMs: 5_000,
            sendQueueByteCapacity: 1);

        await connection.SendAsync(new byte[1], CancellationToken.None);
        GetCapacitySignal(connection).Should().BeNull("capacity signals should not be allocated on the uncontended path");

        var waiters = Enumerable.Range(0, 16)
            .Select(_ => connection.SendAsync(new byte[1], CancellationToken.None).AsTask())
            .ToArray();
        await WaitForConditionAsync(() => GetCapacitySignal(connection) is not null);

        connection.StartSendLoop(CancellationToken.None);
        await Task.WhenAll(waiters).WaitAsync(TimeSpan.FromSeconds(3));
        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(3));

        connection.PendingBytes.Should().Be(0);
        GetCapacitySignal(connection).Should().BeNull();
        transport.SendCount.Should().Be(17);
    }

    [Test]
    public async Task CapacitySignal_CanceledWaiter_DoesNotChangeReservedByteAccounting()
    {
        await using var transport = new SynchronousSendConnection();
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 4,
            sendEnqueueTimeoutMs: 5_000,
            sendQueueByteCapacity: 1);
        await connection.SendAsync(new byte[1], CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        var waitingSend = connection.SendAsync(new byte[1], cancellation.Token).AsTask();
        await WaitForConditionAsync(() => GetCapacitySignal(connection) is not null);
        cancellation.Cancel();

        await FluentActions.Awaiting(async () => await waitingSend)
            .Should().ThrowAsync<OperationCanceledException>();
        connection.PendingBytes.Should().Be(1);

        connection.StartSendLoop(CancellationToken.None);
        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(3));
        connection.PendingBytes.Should().Be(0);
        GetCapacitySignal(connection).Should().BeNull();
    }

    [Test]
    public async Task CapacitySignal_PartialRelease_KeepsWriterBlockedUntilEnoughBytesAreAvailable()
    {
        await using var transport = new SteppedSendConnection();
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 4,
            sendEnqueueTimeoutMs: 5_000,
            sendQueueByteCapacity: 4);
        await connection.SendAsync(new byte[2], CancellationToken.None);
        await connection.SendAsync(new byte[2], CancellationToken.None);
        var waitingSend = connection.SendAsync(new byte[3], CancellationToken.None).AsTask();
        await WaitForConditionAsync(() => GetCapacitySignal(connection) is not null);

        connection.StartSendLoop(CancellationToken.None);
        await transport.WaitForSendAsync(1);
        transport.ReleaseNext();
        await transport.WaitForSendAsync(2);

        waitingSend.IsCompleted.Should().BeFalse("releasing two bytes leaves only two bytes available");
        connection.PendingBytes.Should().Be(2);
        GetCapacitySignal(connection).Should().NotBeNull();

        transport.ReleaseNext();
        await waitingSend.WaitAsync(TimeSpan.FromSeconds(3));
        await transport.WaitForSendAsync(3);
        connection.PendingBytes.Should().Be(3);

        transport.ReleaseNext();
        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(3));
        connection.PendingBytes.Should().Be(0);
        GetCapacitySignal(connection).Should().BeNull();
    }

    [Test]
    public async Task SendLoop_AfterSynchronousWrites_TimesOutCancellationIgnoringTransportAndDefersBufferReturn()
    {
        await using var transport = new SequencedSendConnection(synchronousSends: 2, SendTerminalBehavior.Blocked);
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 4,
            sendEnqueueTimeoutMs: 50,
            sendQueueByteCapacity: 16);
        await connection.SendAsync(new byte[1], CancellationToken.None);
        await connection.SendAsync(new byte[1], CancellationToken.None);
        await connection.SendAsync(new byte[1], CancellationToken.None);

        connection.StartSendLoop(CancellationToken.None);
        await transport.TerminalSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await FluentActions.Awaiting(async () => await connection.SendLoop!)
            .Should().ThrowAsync<BoltTransportSendTimeoutException>();
        connection.TransportSendTimeoutCount.Should().Be(1);
        connection.PendingBytes.Should().Be(1, "the transport may still be reading the final buffer");

        transport.Release();
        await WaitForConditionAsync(() => connection.PendingBytes == 0);
    }

    [Test]
    public async Task SendLoop_SynchronousTransportFailure_ReleasesBufferAndRecordsFailure()
    {
        await using var transport = new SequencedSendConnection(synchronousSends: 0, SendTerminalBehavior.Faulted);
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 1,
            sendEnqueueTimeoutMs: 1_000);
        await connection.SendAsync(new byte[1], CancellationToken.None);

        connection.StartSendLoop(CancellationToken.None);

        await FluentActions.Awaiting(async () => await connection.SendLoop!)
            .Should().ThrowAsync<BoltTransportSendException>();
        connection.PendingBytes.Should().Be(0);
        connection.TransportSendFailureCount.Should().Be(1);
    }

    [Test]
    public async Task SendLoop_AfterSynchronousWrite_ConnectionCancellationDefersBufferUntilTransportCompletes()
    {
        await using var transport = new SequencedSendConnection(synchronousSends: 1, SendTerminalBehavior.Blocked);
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 2,
            sendEnqueueTimeoutMs: 5_000);
        using var cancellation = new CancellationTokenSource();
        await connection.SendAsync(new byte[1], CancellationToken.None);
        await connection.SendAsync(new byte[1], CancellationToken.None);
        connection.StartSendLoop(cancellation.Token);
        await transport.TerminalSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        cancellation.Cancel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(3));

        connection.SendFailure.Should().BeNull();
        connection.PendingBytes.Should().Be(1);
        transport.Release();
        await WaitForConditionAsync(() => connection.PendingBytes == 0);
    }

    private static object? GetCapacitySignal(BoltHubConnection connection) =>
        typeof(BoltHubConnection)
            .GetField("_pendingByteCapacityChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(connection);

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("Condition was not met before timeout.");
            await Task.Delay(10);
        }
    }

    private sealed class SynchronousSendConnection : IBoltConnection
    {
        public int SendCount { get; private set; }
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            SendCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            ValueTask.FromResult((0, true));
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SteppedSendConnection : IBoltConnection
    {
        private readonly Queue<TaskCompletionSource> _pending = new();
        private readonly object _sync = new();
        private int _sendCount;

        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_sync)
            {
                _pending.Enqueue(completion);
                _sendCount++;
            }

            return new ValueTask(completion.Task);
        }

        public Task WaitForSendAsync(int expected) => WaitForConditionAsync(() => Volatile.Read(ref _sendCount) >= expected);

        public void ReleaseNext()
        {
            TaskCompletionSource completion;
            lock (_sync)
                completion = _pending.Dequeue();
            completion.TrySetResult();
        }

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            ValueTask.FromResult((0, true));
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync()
        {
            lock (_sync)
            {
                while (_pending.TryDequeue(out var completion))
                    completion.TrySetResult();
            }
            return ValueTask.CompletedTask;
        }
    }

    private enum SendTerminalBehavior
    {
        Blocked,
        Faulted
    }

    private sealed class SequencedSendConnection(int synchronousSends, SendTerminalBehavior terminalBehavior)
        : IBoltConnection
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _sendCount;

        public TaskCompletionSource TerminalSendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _sendCount) <= synchronousSends)
                return ValueTask.CompletedTask;

            TerminalSendStarted.TrySetResult();
            return terminalBehavior == SendTerminalBehavior.Faulted
                ? ValueTask.FromException(new IOException("Synthetic synchronous transport failure."))
                : new ValueTask(_release.Task);
        }

        public void Release() => _release.TrySetResult();
        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            ValueTask.FromResult((0, true));
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync()
        {
            Release();
            return ValueTask.CompletedTask;
        }
    }
}
