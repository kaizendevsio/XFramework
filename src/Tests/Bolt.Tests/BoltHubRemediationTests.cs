using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using Bolt.Protocol.Transport;
using Bolt.Server;
using Bolt.Server.Durable;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(15000)]
public sealed class BoltHubRemediationTests
{
    [Test]
    public async Task SendAsync_QueuedBytesAtCapacity_WaitsThenTimesOutWithoutRetainingAnotherFrame()
    {
        await using var transport = new BlockingSendConnection();
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 8,
            sendEnqueueTimeoutMs: 1_000,
            sendQueueByteCapacity: 8);

        await connection.SendAsync(new byte[8], CancellationToken.None);

        var act = async () => await connection.SendAsync(new byte[1], CancellationToken.None);

        await act.Should().ThrowAsync<BoltSendEnqueueTimeoutException>();
        connection.PendingBytes.Should().Be(8);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        connection.StartSendLoop(cts.Token);
        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(3));
        await WaitForConditionAsync(() => Task.FromResult(connection.PendingBytes == 0));
        connection.PendingBytes.Should().Be(0);
    }

    [Test]
    public async Task SendAsync_FrameLargerThanByteCapacity_RejectsImmediately()
    {
        await using var transport = new BlockingSendConnection();
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 8,
            sendEnqueueTimeoutMs: 1_000,
            sendQueueByteCapacity: 8);

        var act = async () => await connection.SendAsync(new byte[9], CancellationToken.None);

        await act.Should().ThrowAsync<BoltSendQueueByteCapacityExceededException>();
        connection.PendingBytes.Should().Be(0);
    }

    [Test]
    public async Task SendAsync_QueuedBytesAtCapacity_ResumesWhenPhysicalSendReleasesCapacity()
    {
        await using var transport = new BlockingSendConnection();
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 8,
            sendEnqueueTimeoutMs: 5_000,
            sendQueueByteCapacity: 8);
        using var cts = new CancellationTokenSource();
        connection.StartSendLoop(cts.Token);

        await connection.SendAsync(new byte[8], CancellationToken.None);
        await transport.SendStarted.WaitAsync(TimeSpan.FromSeconds(3));

        var waitingSend = connection.SendAsync(new byte[1], CancellationToken.None).AsTask();
        await Task.Delay(25);
        waitingSend.IsCompleted.Should().BeFalse();

        transport.ReleaseWrites();
        await waitingSend.WaitAsync(TimeSpan.FromSeconds(3));
        await WaitForConditionAsync(() => Task.FromResult(connection.PendingBytes == 0));

        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task SendAsync_OwnedWriterEnqueueTimeout_ReleasesDetachedBufferAccounting()
    {
        await using var transport = new BlockingSendConnection();
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 1,
            sendEnqueueTimeoutMs: 50,
            sendQueueByteCapacity: 64);

        await connection.SendAsync(new byte[4], CancellationToken.None);
        using var writer = new RentedBufferWriter(4);
        writer.GetSpan(4)[..4].Fill(1);
        writer.Advance(4);

        var act = async () => await connection.SendAsync(writer, CancellationToken.None);

        await act.Should().ThrowAsync<BoltSendEnqueueTimeoutException>();
        connection.PendingBytes.Should().Be(4);

        using var stopped = new CancellationTokenSource();
        stopped.Cancel();
        connection.StartSendLoop(stopped.Token);
        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(3));
        connection.PendingBytes.Should().Be(0);
    }

    [Test]
    public async Task DurableReplay_DeferredBytesWouldExceedCapacity_DoesNotRetainAnotherPayload()
    {
        var durableOptions = Options.Create(new DurableQueueOptions
        {
            MaxReplayDeferredBytesPerSubscription = 2
        });
        var durableStore = new InMemoryDurableQueueStore(
            durableOptions,
            NullLogger<InMemoryDurableQueueStore>.Instance);
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions(),
            durableStore,
            durableOptions);
        await using var transport = new BlockingSendConnection();
        var owner = new BoltHubConnection(transport);
        var stateType = typeof(BoltServer).GetNestedType(
            "DurableReplayState",
            BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(stateType, owner)!;
        var handle = typeof(BoltServer).GetMethod(
            "HandleEventDuringReplay",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        handle.Invoke(server, [state, 1L, new ReadOnlyMemory<byte>(new byte[] { 1, 2 })]);
        handle.Invoke(server, [state, 2L, new ReadOnlyMemory<byte>(new byte[] { 3 })]);

        ((long)stateType.GetProperty("DeferredBytes")!.GetValue(state)!).Should().Be(2);
        var events = stateType.GetProperty("DeferredEvents")!.GetValue(state)!;
        ((int)events.GetType().GetProperty("Count")!.GetValue(events)!).Should().Be(1);
    }

    [Test]
    public async Task DurableSubscription_ActorTokenExpiry_RetiresCachedAuthorization()
    {
        var durableOptions = Options.Create(new DurableQueueOptions());
        var durableStore = new InMemoryDurableQueueStore(
            durableOptions,
            NullLogger<InMemoryDurableQueueStore>.Instance);
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { CleanupIntervalSeconds = 1 },
            durableStore,
            durableOptions);
        await using var transport = new ChannelBoltConnection();
        var serverTask = server.HandleConnectionAsync(transport, CancellationToken.None);
        transport.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "expiry-client", "ExpiryClient")));
        await transport.WaitForSentFramesAsync(1);

        const string topic = "authorization.expiry";
        const string subscriberId = "expiry-subscriber";
        var topicHash = BoltCodec.Fnv1aHash(topic);
        var actorToken = CreateUnsignedJwt(DateTimeOffset.UtcNow.AddSeconds(2));
        transport.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(
            writer,
            topic,
            subscriberId,
            durable: true,
            actorToken)));

        await WaitForConditionAsync(() => Task.FromResult(
            HasDurableBinding(server, topicHash, subscriberId)));
        await WaitForConditionAsync(() => Task.FromResult(
            !HasDurableBinding(server, topicHash, subscriberId)));

        transport.Complete();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task ServiceRouteMutation_DisconnectDuringRegistration_PreservesLiveRoutes()
    {
        using var server = new BoltServer(NullLogger<BoltServer>.Instance, new BoltServerOptions());
        await using var caller = new ChannelBoltConnection();
        await using var first = new ChannelBoltConnection();
        await using var second = new ChannelBoltConnection();
        await using var replacement = new ChannelBoltConnection();
        var callerTask = server.HandleConnectionAsync(caller, CancellationToken.None);
        var firstTask = server.HandleConnectionAsync(first, CancellationToken.None);
        var secondTask = server.HandleConnectionAsync(second, CancellationToken.None);
        var replacementTask = server.HandleConnectionAsync(replacement, CancellationToken.None);

        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "route-caller", "RouteCaller")));
        first.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "route-service", "RouteService")));
        second.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "route-service", "RouteService")));
        await Task.WhenAll(
            caller.WaitForSentFramesAsync(1),
            first.WaitForSentFramesAsync(1),
            second.WaitForSentFramesAsync(1));

        first.Complete();
        replacement.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "route-service", "RouteService")));
        await Task.WhenAll(firstTask, replacement.WaitForSentFramesAsync(1));

        var recipientHash = BoltCodec.Fnv1aHash("route-service");
        var senderHash = BoltCodec.Fnv1aHash("route-caller");
        for (var i = 0; i < 12; i++)
        {
            caller.Enqueue(WriteFrame(writer => BoltCodec.WritePush(
                writer,
                Guid.NewGuid(),
                recipientHash,
                senderHash,
                BoltCodec.Fnv1aHash("route-test"),
                [1])));
        }

        await WaitForConditionAsync(() =>
            Task.FromResult(
                CountLogicalFrames(second.SentFrames) +
                CountLogicalFrames(replacement.SentFrames) >= 14));
        second.SentFrames.Count.Should().BeGreaterThan(1);
        replacement.SentFrames.Count.Should().BeGreaterThan(1);

        caller.Complete();
        second.Complete();
        replacement.Complete();
        await Task.WhenAll(callerTask, secondTask, replacementTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task MediaFrame_CallHeld_IsRejectedUntilCallIsUnheld()
    {
        var logger = new HeldMediaLogger();
        using var server = new BoltServer(
            logger,
            new BoltServerOptions { MediaEnabled = true });
        await using var caller = new ChannelBoltConnection();
        await using var callee = new ChannelBoltConnection();
        var callerTask = server.HandleConnectionAsync(caller, CancellationToken.None);
        var calleeTask = server.HandleConnectionAsync(callee, CancellationToken.None);
        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "hold-caller", "HoldCaller")));
        callee.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "hold-callee", "HoldCallee")));
        await Task.WhenAll(caller.WaitForSentFramesAsync(1), callee.WaitForSentFramesAsync(1));

        var callId = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var initiatePayload = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(initiatePayload, BoltCodec.Fnv1aHash("hold-callee"));
        caller.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteCallSignal(writer, callId, SignalType.Initiate, initiatePayload)));
        await Task.WhenAll(caller.WaitForSentFramesAsync(2), callee.WaitForSentFramesAsync(2));
        callee.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteCallSignal(writer, callId, SignalType.Answer, ReadOnlySpan<byte>.Empty)));
        await caller.WaitForSentFramesAsync(3);
        caller.Enqueue(WriteMediaConfig(callId, streamId));
        await callee.WaitForSentFramesAsync(3);

        caller.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteCallSignal(writer, callId, SignalType.Hold, ReadOnlySpan<byte>.Empty)));
        await callee.WaitForSentFramesAsync(4);
        caller.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteMediaFrame(writer, streamId, 1, 960, 0, new byte[] { 1, 2, 3 })));
        await logger.RejectionLogged.WaitAsync(TimeSpan.FromSeconds(3));
        callee.SentFrames.Should().HaveCount(4);

        caller.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteCallSignal(writer, callId, SignalType.Unhold, ReadOnlySpan<byte>.Empty)));
        await callee.WaitForSentFramesAsync(5);
        caller.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteMediaFrame(writer, streamId, 2, 1_920, 0, new byte[] { 4, 5, 6 })));
        await callee.WaitForSentFramesAsync(6);
        callee.SentFrames.ToArray()[5][0].Should().Be((byte)FrameType.MediaFrame);

        caller.Complete();
        callee.Complete();
        await Task.WhenAll(callerTask, calleeTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    private static byte[] WriteFrame(Action<IBufferWriter<byte>> write)
    {
        var writer = new ArrayBufferWriter<byte>();
        write(writer);
        return writer.WrittenSpan.ToArray();
    }

    private static int CountLogicalFrames(IEnumerable<byte[]> messages)
    {
        var count = 0;
        foreach (var message in messages)
        {
            count += BoltCodec.TryReadBatch(message, out var batch) ? batch.Count : 1;
        }
        return count;
    }

    private static bool HasDurableBinding(BoltServer server, int topicHash, string subscriberId)
    {
        var bindings = (ConcurrentDictionary<(int TopicHash, string SubscriberId), BoltHubConnection>)
            typeof(BoltServer)
                .GetField("_liveDurableConnections", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(server)!;
        return bindings.ContainsKey((topicHash, subscriberId));
    }

    private static string CreateUnsignedJwt(DateTimeOffset expiration)
    {
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var header = Encode("{\"alg\":\"none\"}");
        var payload = Encode(JsonSerializer.Serialize(new { exp = expiration.ToUnixTimeSeconds() }));
        return $"{header}.{payload}.signature";
    }

    private static byte[] WriteMediaConfig(Guid callId, Guid streamId) =>
        WriteFrame(writer => BoltCodec.WriteMediaConfig(
            writer,
            streamId,
            callId,
            MediaType.Audio,
            CodecId.Opus,
            48_000,
            1,
            64,
            0,
            ReadOnlySpan<byte>.Empty));

    private static async Task WaitForConditionAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met before timeout.");
    }

    private sealed class HeldMediaLogger : ILogger<BoltServer>
    {
        private readonly TaskCompletionSource _rejectionLogged =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RejectionLogged => _rejectionLogged.Task;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, exception).StartsWith(
                    "Rejected media traffic while call is held.",
                    StringComparison.Ordinal))
            {
                _rejectionLogged.TrySetResult();
            }
        }
    }

    private sealed class ChannelBoltConnection : IBoltConnection
    {
        private readonly Channel<byte[]> _incoming = Channel.CreateUnbounded<byte[]>();
        private int _connected = 1;

        public ConcurrentQueue<byte[]> SentFrames { get; } = new();
        public bool SupportsDatagrams => false;
        public bool IsConnected => Volatile.Read(ref _connected) != 0;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public void Enqueue(byte[] frame) => _incoming.Writer.TryWrite(frame).Should().BeTrue();
        public void Complete() => _incoming.Writer.TryComplete();

        public async Task WaitForSentFramesAsync(int expected) =>
            await WaitForConditionAsync(() => Task.FromResult(SentFrames.Count >= expected));

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            SentFrames.Enqueue(data.ToArray());
            return ValueTask.CompletedTask;
        }

        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default)
        {
            while (await _incoming.Reader.WaitToReadAsync(ct))
            {
                if (!_incoming.Reader.TryRead(out var frame))
                    continue;
                frame.CopyTo(buffer);
                return (frame.Length, true);
            }

            Interlocked.Exchange(ref _connected, 0);
            return (0, true);
        }

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default)
        {
            Interlocked.Exchange(ref _connected, 0);
            Complete();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _connected, 0);
            Complete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingSendConnection : IBoltConnection
    {
        private readonly TaskCompletionSource _sendStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _writesReleased =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendStarted => _sendStarted.Task;
        public void ReleaseWrites() => _writesReleased.TrySetResult();
        public bool SupportsDatagrams => false;
        public bool IsConnected { get; private set; } = true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            _sendStarted.TrySetResult();
            await _writesReleased.Task.WaitAsync(ct);
        }

        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return (0, true);
        }

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
}
