using System.Collections.Concurrent;
using System.Reflection;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using Bolt.Protocol.Transport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltClientOutboundBatchingTests
{
    [Test]
    public async Task QueuedBatchableFrames_AreWrittenAsOneBatch()
    {
        var transport = new RecordingTransport(blockFirstWrite: true);
        var connection = new BoltConnection(transport);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(connection);

        var first = client.PushAsync("receiver", "first", new byte[] { 1 }).AsTask();
        await transport.FirstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var queued = Enumerable.Range(0, 3)
            .Select(index => client.PushAsync("receiver", $"queued-{index}", new byte[] { 2, 3 }).AsTask())
            .ToArray();
        await WaitUntilAsync(() => connection.PendingSends == 4);

        transport.ReleaseFirstWrite();
        await Task.WhenAll(queued.Prepend(first));
        await WaitUntilAsync(() => connection.PendingSends == 0);

        transport.Frames.Should().HaveCount(2);
        transport.Frames[0][0].Should().Be((byte)FrameType.Push);
        transport.Frames[1][0].Should().Be((byte)FrameType.Batch);
        BoltCodec.TryReadBatch(transport.Frames[1], out var batch).Should().BeTrue();
        batch.Count.Should().Be(3);
    }

    [Test]
    public async Task IneligibleNextFrame_IsLeftInQueueAndOrderingIsPreserved()
    {
        var transport = new RecordingTransport(blockFirstWrite: true);
        var connection = new BoltConnection(transport);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(connection);

        var first = client.PushAsync("receiver", "first", new byte[] { 1 }).AsTask();
        await transport.FirstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await connection.SendAsync(CreatePushFrame(2), CancellationToken.None);
        await connection.SendAsync(CreatePushFrame(3), CancellationToken.None);
        await connection.SendAsync(CreateRegisterFrame(), CancellationToken.None);
        await WaitUntilAsync(() => connection.PendingSends == 4);

        transport.ReleaseFirstWrite();
        await first;
        await WaitUntilAsync(() => connection.PendingSends == 0);

        transport.Frames.Should().HaveCount(3);
        transport.Frames[0][0].Should().Be((byte)FrameType.Push);
        transport.Frames[1][0].Should().Be((byte)FrameType.Batch);
        transport.Frames[2][0].Should().Be((byte)FrameType.Register);
        BoltCodec.TryReadBatch(transport.Frames[1], out var batch).Should().BeTrue();
        batch.Count.Should().Be(2);
    }

    [Test]
    public async Task MalformedBatchableFrame_RemainsStandaloneAndDoesNotPoisonFollowingFrame()
    {
        var transport = new RecordingTransport(blockFirstWrite: true);
        var connection = new BoltConnection(transport);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(connection);

        var first = client.PushAsync("receiver", "first", new byte[] { 1 }).AsTask();
        await transport.FirstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var malformedClose = new byte[BoltCodec.StreamCloseSize - 1];
        malformedClose[0] = (byte)FrameType.StreamClose;
        await connection.SendAsync(malformedClose, CancellationToken.None);
        await connection.SendAsync(CreateRequestCancelFrame(), CancellationToken.None);
        await WaitUntilAsync(() => connection.PendingSends == 3);

        transport.ReleaseFirstWrite();
        await first;
        await WaitUntilAsync(() => connection.PendingSends == 0);

        transport.Frames.Should().HaveCount(3);
        transport.Frames[1].Should().Equal(malformedClose);
        transport.Frames[2][0].Should().Be((byte)FrameType.RequestCancel);
    }

    [Test]
    public async Task OversizedNextFrame_IsNotAddedToBatch()
    {
        var transport = new RecordingTransport(blockFirstWrite: true);
        var connection = new BoltConnection(transport);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(connection);

        var first = client.PushAsync("receiver", "first", new byte[] { 1 }).AsTask();
        await transport.FirstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await connection.SendAsync(CreatePushFrame(262_100), CancellationToken.None);
        await connection.SendAsync(CreatePushFrame(4), CancellationToken.None);
        await WaitUntilAsync(() => connection.PendingSends == 3);

        transport.ReleaseFirstWrite();
        await first;
        await WaitUntilAsync(() => connection.PendingSends == 0);

        transport.Frames.Should().HaveCount(3);
        transport.Frames.Should().OnlyContain(frame => frame[0] == (byte)FrameType.Push);
    }

    [Test]
    public async Task CallerCancellationAfterEnqueue_DoesNotCancelPhysicalBatchWrite()
    {
        var transport = new RecordingTransport(blockFirstWrite: true);
        var connection = new BoltConnection(transport);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(connection);

        var first = client.PushAsync("receiver", "first", new byte[] { 1 }).AsTask();
        await transport.FirstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        using var firstCts = new CancellationTokenSource();
        using var secondCts = new CancellationTokenSource();
        var canceledFirst = client.PushAsync("receiver", "canceled-1", new byte[] { 2 }, firstCts.Token).AsTask();
        var canceledSecond = client.PushAsync("receiver", "canceled-2", new byte[] { 3 }, secondCts.Token).AsTask();
        await WaitUntilAsync(() => connection.PendingSends == 3);

        firstCts.Cancel();
        secondCts.Cancel();
        AssertCanceled(canceledFirst);
        AssertCanceled(canceledSecond);

        transport.ReleaseFirstWrite();
        await first;
        await WaitUntilAsync(() => connection.PendingSends == 0);
        transport.Frames.Should().HaveCount(2);
        transport.Frames[1][0].Should().Be((byte)FrameType.Batch);
    }

    [Test]
    public async Task BatchWriteFailure_CompletesEveryQueuedItem()
    {
        var transport = new RecordingTransport(blockFirstWrite: true, failWriteNumber: 2);
        var connection = new BoltConnection(transport);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(connection);
        SetField(client, "_disposed", true);

        var first = client.PushAsync("receiver", "first", new byte[] { 1 }).AsTask();
        await transport.FirstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var failed = Enumerable.Range(0, 3)
            .Select(index => client.PushAsync("receiver", $"failed-{index}", new byte[] { 2 }).AsTask())
            .ToArray();
        await WaitUntilAsync(() => connection.PendingSends == 4);

        transport.ReleaseFirstWrite();
        await first;
        foreach (var task in failed)
            Assert.ThrowsAsync<IOException>(async () => await task);

        await WaitUntilAsync(() => connection.PendingSends == 0);
        transport.Frames.Should().HaveCount(2);
        transport.Frames[1][0].Should().Be((byte)FrameType.Batch);
    }

    private static BoltClient CreateAttachedClient(BoltConnection connection)
    {
        var client = new BoltClient(
            new Uri("ws://localhost:1/bolt"),
            "batch_sender",
            "BatchSender",
            new BoltClientOptions(),
            NullLogger<BoltClient>.Instance);

        var connections = (List<BoltConnection>)GetField(client, "_connections");
        connections.Add(connection);
        SetField(client, "_isRegistered", true);
        typeof(BoltClient)
            .GetMethod("ObserveConnection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(client, [connection]);
        return client;
    }

    private static byte[] CreatePushFrame(int payloadLength)
    {
        using var writer = new RentedBufferWriter(payloadLength + BoltCodec.RequestHeaderSize);
        BoltCodec.WritePush(writer, Guid.NewGuid(), 1, 2, 3, new byte[payloadLength]);
        return writer.WrittenSpan.ToArray();
    }

    private static byte[] CreateRegisterFrame()
    {
        using var writer = new RentedBufferWriter(128);
        BoltCodec.WriteRegister(writer, "registration", "test");
        return writer.WrittenSpan.ToArray();
    }

    private static object GetField(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static byte[] CreateRequestCancelFrame()
    {
        var writer = new RentedBufferWriter(BoltCodec.RequestCancelSize);
        try
        {
            BoltCodec.WriteRequestCancel(writer, Guid.NewGuid());
            return writer.WrittenSpan.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static void AssertCanceled(Task task) =>
        Assert.ThrowsAsync<OperationCanceledException>(async () => await task);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        condition().Should().BeTrue();
    }

    private sealed class RecordingTransport(bool blockFirstWrite, int failWriteNumber = 0) : IBoltConnection
    {
        private readonly TaskCompletionSource _firstWriteRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _writeCount;

        public bool SupportsDatagrams => false;
        public bool IsConnected { get; private set; } = true;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public TaskCompletionSource FirstWriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<byte[]> Frames { get; } = [];

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            var writeNumber = Interlocked.Increment(ref _writeCount);
            lock (Frames)
                Frames.Add(data.ToArray());
            if (failWriteNumber == writeNumber)
                return ValueTask.FromException(new IOException("synthetic batch write failure"));

            if (blockFirstWrite && writeNumber == 1)
            {
                FirstWriteStarted.TrySetResult();
                return new ValueTask(_firstWriteRelease.Task);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default) => ValueTask.FromResult((0, true));

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default)
        {
            IsConnected = false;
            _firstWriteRelease.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            _firstWriteRelease.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public void ReleaseFirstWrite() => _firstWriteRelease.TrySetResult();
    }
}
