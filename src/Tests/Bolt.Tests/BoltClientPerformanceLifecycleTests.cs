using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Threading.Channels;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using Bolt.Protocol.Transport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltClientPerformanceLifecycleTests
{
    private static readonly MethodInfo RegisterRpcCancellationMethod = typeof(PooledRpcCall)
        .GetMethod("RegisterCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Test]
    public async Task InvokeAsync_OversizedPayload_Returns413WithoutSelectingConnection()
    {
        await using var client = CreateClient(new BoltClientOptions { MaxLargeRpcPayloadBytes = 1024 });

        var result = await client.InvokeAsync("receiver", "command", new byte[1025]);

        result.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Test]
    public async Task PendingRpcCancellation_RemovesLookupBeforePooledCallCanBeReused()
    {
        await using var client = CreateClient(new BoltClientOptions());
        var pendingCalls = GetPendingCalls(client);
        var requestId = Guid.NewGuid();
        var call = PooledRpcCall.Rent(runContinuationsAsynchronously: true);
        var response = call.GetTask();
        pendingCalls[requestId] = call;
        using var cancellation = new CancellationTokenSource();

        RegisterRpcCancellationMethod.Invoke(call, [cancellation.Token, client, requestId]);
        cancellation.Cancel();

        pendingCalls.Should().NotContainKey(requestId);
        await FluentActions.Awaiting(async () => await response)
            .Should().ThrowAsync<OperationCanceledException>();

        var replacement = PooledRpcCall.Rent();
        var replacementResponse = replacement.GetTask();
        replacement.SetResult(new BoltRpcResponse { StatusCode = HttpStatusCode.Accepted });
        (await replacementResponse).StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task PooledRpcCall_ResponseAndCancellationRace_DoesNotCorruptReusedCall()
    {
        const int iterations = 2_000;
        await using var client = CreateClient(new BoltClientOptions());
        var pendingCalls = GetPendingCalls(client);
        var responses = 0;
        var cancellations = 0;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var requestId = Guid.NewGuid();
            var call = PooledRpcCall.Rent(runContinuationsAsynchronously: true);
            var response = call.GetTask();
            pendingCalls[requestId] = call;
            using var cancellation = new CancellationTokenSource();
            RegisterRpcCancellationMethod.Invoke(call, [cancellation.Token, client, requestId]);
            using var start = new ManualResetEventSlim();

            var responseRace = Task.Run(() =>
            {
                start.Wait();
                if (pendingCalls.TryRemove(requestId, out var pendingCall))
                {
                    pendingCall.SetResult(new BoltRpcResponse
                    {
                        StatusCode = HttpStatusCode.OK,
                        Data = new byte[] { 1 }
                    });
                }
            });
            var cancellationRace = Task.Run(() =>
            {
                start.Wait();
                cancellation.Cancel();
            });

            start.Set();
            await Task.WhenAll(responseRace, cancellationRace);

            try
            {
                var result = await response;
                result.StatusCode.Should().Be(HttpStatusCode.OK);
                responses++;
            }
            catch (OperationCanceledException)
            {
                cancellations++;
            }

            pendingCalls.Should().NotContainKey(requestId);

            var replacement = PooledRpcCall.Rent(runContinuationsAsynchronously: true);
            ReferenceEquals(replacement, call).Should().BeTrue(
                "the race must exercise the same PooledRpcCall instance after it returns to the pool");
            var replacementResponse = replacement.GetTask();
            var replacementRequestId = Guid.NewGuid();
            pendingCalls[replacementRequestId] = replacement;
            pendingCalls.TryRemove(requestId, out var lateCall).Should().BeFalse(
                "a late response must not retain the pooled call after cancellation or completion");
            lateCall.Should().BeNull();
            pendingCalls.TryRemove(replacementRequestId, out var replacementCall).Should().BeTrue();
            replacementCall.Should().BeSameAs(replacement);
            replacementCall!.SetResult(new BoltRpcResponse
            {
                StatusCode = HttpStatusCode.Accepted,
                Data = BitConverter.GetBytes(iteration)
            });
            var replacementResult = await replacementResponse;
            replacementResult.StatusCode.Should().Be(HttpStatusCode.Accepted);
            BitConverter.ToInt32(replacementResult.Data.Span).Should().Be(iteration);
        }

        (responses + cancellations).Should().Be(iterations);
        responses.Should().BeGreaterThan(0);
        cancellations.Should().BeGreaterThan(0);
        pendingCalls.Should().BeEmpty();
    }

    [Test]
    public async Task InternalLargeRpc_InstallsCollectorBeforePayloadFramesAreDispatched()
    {
        var transport = new CountingBoltConnection();
        var connection = new BoltConnection(transport, enableBatching: false);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(connection, new BoltClientOptions());
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.RegisterHandler("collect", (payload, _) =>
        {
            received.TrySetResult(payload.ToArray());
            return Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty));
        });
        InvokePrivate(client, "RegisterLargeRpcStreamHandler");

        var streamId = Guid.NewGuid();
        await DispatchAsync(client, connection, WriteFrame(writer =>
            BoltCodec.WriteStreamOpen(
                writer,
                streamId,
                BoltCodec.Fnv1aHash("receiver"),
                BoltCodec.Fnv1aHash("__bolt_large_rpc__"))));

        var stream = GetActiveStreams(client)[streamId];
        var header = new byte[28];
        Guid.NewGuid().TryWriteBytes(header);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
            header.AsSpan(16),
            BoltCodec.Fnv1aHash("collect"));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20), 3);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), 42);

        await DispatchAsync(client, connection, WriteFrame(writer =>
            BoltCodec.WriteStreamData(writer, streamId, header)));
        await DispatchAsync(client, connection, WriteFrame(writer =>
            BoltCodec.WriteStreamData(writer, streamId, new byte[] { 1, 2, 3 })));

        GetInboundChannel(stream).Reader.TryRead(out _).Should().BeFalse(
            "the built-in collector should receive chunks directly instead of queueing copied arrays");

        await DispatchAsync(client, connection, WriteFrame(writer =>
            BoltCodec.WriteStreamClose(writer, streamId, HttpStatusCode.OK)));

        (await received.Task.WaitAsync(TimeSpan.FromSeconds(2))).Should().Equal(1, 2, 3);
    }

    [Test]
    public async Task DisposeAsync_WhenInboundCancellationCompletesConcurrently_IsIdempotent()
    {
        var client = CreateClient(new BoltClientOptions());
        var cancellation = new CancellationTokenSource();
        var cancellations = (ConcurrentDictionary<Guid, CancellationTokenSource>)GetField(
            client,
            "_inboundRequestCancellations");
        cancellations.TryAdd(Guid.NewGuid(), cancellation).Should().BeTrue();
        cancellation.Dispose();

        await client.DisposeAsync();
        await client.DisposeAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task InternalLargeRpc_MissingOrMalformedMetadata_ReleasesAllState(bool sendMalformedMetadata)
    {
        var transport = new CountingBoltConnection();
        var connection = new BoltConnection(transport, enableBatching: false);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(connection, new BoltClientOptions());
        InvokePrivate(client, "RegisterLargeRpcStreamHandler");

        var streamId = await OpenInternalLargeRpcStreamAsync(client, connection);
        if (sendMalformedMetadata)
        {
            await DispatchAsync(client, connection, WriteFrame(writer =>
                BoltCodec.WriteStreamData(writer, streamId, new byte[27])));
        }

        await DispatchStreamCloseAsync(client, connection, streamId);
        await AssertLargeRpcStateReleasedAsync(client);
    }

    [TestCase(2049, 4096, HttpStatusCode.RequestEntityTooLarge)]
    [TestCase(1024, 512, HttpStatusCode.TooManyRequests)]
    public async Task InternalLargeRpc_DeclarationRejection_ReleasesAllState(
        int declaredBytes,
        int bufferBudgetBytes,
        HttpStatusCode expectedStatus)
    {
        var transport = new RecordingBoltConnection();
        var connection = new BoltConnection(transport, enableBatching: false);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(
            connection,
            new BoltClientOptions
            {
                MaxLargeRpcPayloadBytes = 2048,
                MaxBufferedLargeRpcBytes = bufferBudgetBytes
            });
        InvokePrivate(client, "RegisterLargeRpcStreamHandler");

        var streamId = await OpenInternalLargeRpcStreamAsync(client, connection);
        await DispatchAsync(client, connection, WriteFrame(writer =>
            BoltCodec.WriteStreamData(
                writer,
                streamId,
                CreateLargeRpcRequestHeader(Guid.NewGuid(), "unused", declaredBytes))));

        await WaitUntilAsync(() => transport.ResponseStatuses.Contains(expectedStatus));
        await AssertLargeRpcStateReleasedAsync(client);
    }

    [Test]
    public async Task InternalLargeRpc_EarlyCloseAfterPartialPayload_ReleasesReservationAndSkipsHandler()
    {
        var transport = new CountingBoltConnection();
        var connection = new BoltConnection(transport, enableBatching: false);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(connection, new BoltClientOptions());
        var handlerCalls = 0;
        client.RegisterHandler("partial", (_, _) =>
        {
            Interlocked.Increment(ref handlerCalls);
            return Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty));
        });
        InvokePrivate(client, "RegisterLargeRpcStreamHandler");

        var streamId = await OpenInternalLargeRpcStreamAsync(client, connection);
        await DispatchAsync(client, connection, WriteFrame(writer =>
            BoltCodec.WriteStreamData(
                writer,
                streamId,
                CreateLargeRpcRequestHeader(Guid.NewGuid(), "partial", 32))));
        await DispatchAsync(client, connection, WriteFrame(writer =>
            BoltCodec.WriteStreamData(writer, streamId, new byte[8])));
        await DispatchStreamCloseAsync(client, connection, streamId);

        await AssertLargeRpcStateReleasedAsync(client);
        Volatile.Read(ref handlerCalls).Should().Be(0);
    }

    [Test]
    public async Task InternalLargeRpc_ConcurrentOpensWithImmediateChunks_ReleaseAllState()
    {
        const int streamCount = 64;
        var transport = new CountingBoltConnection();
        var connection = new BoltConnection(transport, enableBatching: false);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(
            connection,
            new BoltClientOptions
            {
                MaxActiveStreams = streamCount,
                MaxBufferedLargeRpcBytes = 1024 * 1024
            });
        var handlerCalls = 0;
        client.RegisterHandler("concurrent", (payload, _) =>
        {
            payload.Span.SequenceEqual(new byte[] { 1, 2, 3 }).Should().BeTrue();
            Interlocked.Increment(ref handlerCalls);
            return Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty));
        });
        InvokePrivate(client, "RegisterLargeRpcStreamHandler");

        var streamIds = new Guid[streamCount];
        for (var index = 0; index < streamCount; index++)
            streamIds[index] = await OpenInternalLargeRpcStreamAsync(client, connection);

        await Task.WhenAll(streamIds.Select(async streamId =>
        {
            await DispatchAsync(client, connection, WriteFrame(writer =>
                BoltCodec.WriteStreamData(
                    writer,
                    streamId,
                    CreateLargeRpcRequestHeader(Guid.NewGuid(), "concurrent", 3))));
            await DispatchAsync(client, connection, WriteFrame(writer =>
                BoltCodec.WriteStreamData(writer, streamId, new byte[] { 1, 2, 3 })));
            await DispatchStreamCloseAsync(client, connection, streamId);
        }));

        await WaitUntilAsync(() => Volatile.Read(ref handlerCalls) == streamCount);
        await AssertLargeRpcStateReleasedAsync(client);
    }

    [Test]
    public async Task PhysicalWriteTimeout_StopsCancellationIgnoringTransportAndDefersBufferRelease()
    {
        var transport = new CancellationIgnoringBoltConnection();
        var connection = new BoltConnection(
            transport,
            sendQueueCapacity: 4,
            sendEnqueueTimeoutMs: 50,
            enableBatching: false);
        connection.StartSendLoop(CancellationToken.None);
        var client = CreateAttachedClient(connection, new BoltClientOptions());
        SetField(client, "_disposed", true);

        var push = client.PushAsync("receiver", "command", new byte[] { 1, 2, 3 }).AsTask();
        await transport.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await FluentActions.Awaiting(async () => await push)
            .Should().ThrowAsync<TimeoutException>();
        connection.PendingSends.Should().Be(1,
            "the pooled frame must remain owned until the cancellation-ignoring write actually completes");

        transport.Release();
        await WaitUntilAsync(() => connection.PendingSends == 0);
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(2));
        await client.DisposeAsync();
    }

    [Test]
    public async Task PhysicalWriteDeadline_IsReusableAcrossSuccessfulWrites()
    {
        var transport = new CountingBoltConnection();
        var connection = new BoltConnection(
            transport,
            sendQueueCapacity: 4,
            sendEnqueueTimeoutMs: 1000,
            enableBatching: false);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateAttachedClient(connection, new BoltClientOptions());

        await client.PushAsync("receiver", "first", new byte[] { 1 });
        await client.PushAsync("receiver", "second", new byte[] { 2 });

        connection.SendLoop!.IsCompleted.Should().BeFalse();
        connection.PendingSends.Should().Be(0);
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task PhysicalWriteDeadline_NearBoundaryCompletionsDoNotDoubleReleaseAndTimeoutRetiresConnection()
    {
        const int sendTimeoutMs = 150;
        const int successfulWrites = 12;
        var transport = new ControlledBoundaryBoltConnection();
        var connection = new BoltConnection(
            transport,
            sendQueueCapacity: 32,
            sendEnqueueTimeoutMs: sendTimeoutMs,
            enableBatching: false);
        connection.StartSendLoop(CancellationToken.None);
        var client = CreateAttachedClient(connection, new BoltClientOptions());
        SetField(client, "_disposed", true);

        for (var index = 0; index < successfulWrites; index++)
        {
            var push = client.PushAsync("receiver", $"near-boundary-{index}", new byte[] { 1 }).AsTask();
            var write = await transport.Writes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(sendTimeoutMs - 50);
            write.Complete();
            await push.WaitAsync(TimeSpan.FromSeconds(2));
            connection.PendingSends.Should().Be(0);
            connection.SendLoop!.IsCompleted.Should().BeFalse();
        }

        var timedOutPush = client.PushAsync("receiver", "timeout", new byte[] { 2 }).AsTask();
        var timedOutWrite = await transport.Writes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        await FluentActions.Awaiting(async () => await timedOutPush)
            .Should().ThrowAsync<TimeoutException>();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(2));
        connection.PendingSends.Should().Be(1);

        FluentActions.Invoking(() => connection.SendAsync(new byte[] { 3 }, CancellationToken.None))
            .Should().Throw<InvalidOperationException>();

        timedOutWrite.Complete();
        await WaitUntilAsync(() => connection.PendingSends == 0);
        await Task.Delay(50);
        connection.PendingSends.Should().Be(0, "the timed-out buffer must be released exactly once");
        await client.DisposeAsync();
    }

    [Test]
    public async Task LargeRpcPipeline_PhysicallyWritesCloseOnlyAfterEveryDataWriteCompletes()
    {
        const int chunkSize = 4;
        const int dataFrames = 3;
        var transport = new ControlledBoundaryBoltConnection();
        var connection = new BoltConnection(transport, enableBatching: false);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateClient(new BoltClientOptions
        {
            StreamChunkSize = chunkSize,
            MaxLargeRpcPipelineBytes = 2 * chunkSize,
            EnableBatching = false
        });
        var stream = CreateStream(connection);

        var operation = SendPayloadAndCloseAsync(client, stream, new byte[dataFrames * chunkSize]);
        for (var index = 0; index < dataFrames; index++)
        {
            var write = await transport.Writes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            write.FrameType.Should().Be(FrameType.StreamData);
            write.Complete();
        }

        var closeWrite = await transport.Writes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        closeWrite.FrameType.Should().Be(FrameType.StreamClose);
        transport.CompletedWrites.Should().Be(dataFrames,
            "all pipelined StreamData frames must complete physically before StreamClose starts");
        closeWrite.Complete();

        await operation.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => connection.PendingSends == 0);
        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public void AlignedStreamChunkSize_RemainsPubliclyConfigurable()
    {
        var alignedSize = (256 * 1024) - BoltCodec.StreamDataHeaderSize;
        var options = new BoltClientOptions { StreamChunkSize = alignedSize };

        options.StreamChunkSize.Should().Be(262123);
    }

    [Test]
    public void AsynchronousRpcContinuationMode_DefaultsOnAndCanBeDisabled()
    {
        new BoltClientOptions().RunRpcContinuationsAsynchronously.Should().BeTrue();
        new BoltClientOptions { RunRpcContinuationsAsynchronously = false }
            .RunRpcContinuationsAsynchronously.Should().BeFalse();
    }

    private static BoltClient CreateClient(BoltClientOptions options) => new(
        new Uri("ws://localhost:1/bolt"),
        "performance_client",
        "PerformanceClient",
        options,
        NullLogger<BoltClient>.Instance);

    private static BoltClient CreateAttachedClient(BoltConnection connection, BoltClientOptions options)
    {
        var client = CreateClient(options);
        var connections = (List<BoltConnection>)GetField(client, "_connections");
        connections.Add(connection);
        SetField(client, "_isRegistered", true);
        return client;
    }

    private static ConcurrentDictionary<Guid, PooledRpcCall> GetPendingCalls(BoltClient client) =>
        (ConcurrentDictionary<Guid, PooledRpcCall>)GetField(client, "_pendingCalls");

    private static ConcurrentDictionary<Guid, BoltStream> GetActiveStreams(BoltClient client) =>
        (ConcurrentDictionary<Guid, BoltStream>)GetField(client, "_activeStreams");

    private static Channel<ReadOnlyMemory<byte>> GetInboundChannel(BoltStream stream) =>
        (Channel<ReadOnlyMemory<byte>>)GetField(stream, "_inboundChannel");

    private static BoltStream CreateStream(BoltConnection connection) =>
        (BoltStream)(Activator.CreateInstance(
            typeof(BoltStream),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [Guid.NewGuid(), connection, null, 16],
            culture: null) ?? throw new InvalidOperationException("BoltStream constructor was not found."));

    private static async Task<Guid> OpenInternalLargeRpcStreamAsync(
        BoltClient client,
        BoltConnection connection)
    {
        var streamId = Guid.NewGuid();
        await DispatchAsync(client, connection, WriteFrame(writer =>
            BoltCodec.WriteStreamOpen(
                writer,
                streamId,
                BoltCodec.Fnv1aHash("receiver"),
                BoltCodec.Fnv1aHash("__bolt_large_rpc__"))));
        return streamId;
    }

    private static ValueTask DispatchStreamCloseAsync(
        BoltClient client,
        BoltConnection connection,
        Guid streamId) =>
        DispatchAsync(client, connection, WriteFrame(writer =>
            BoltCodec.WriteStreamClose(writer, streamId, HttpStatusCode.OK)));

    private static byte[] CreateLargeRpcRequestHeader(
        Guid requestId,
        string command,
        int declaredBytes)
    {
        var header = new byte[28];
        requestId.TryWriteBytes(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), BoltCodec.Fnv1aHash(command));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20), declaredBytes);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), 42);
        return header;
    }

    private static async Task AssertLargeRpcStateReleasedAsync(BoltClient client)
    {
        await WaitUntilAsync(() =>
            GetBufferedLargeRpcBytes(client) == 0 &&
            GetPendingCalls(client).IsEmpty &&
            GetActiveStreams(client).IsEmpty &&
            GetCollectionCount(client, "_largeRpcCollectors") == 0);

        GetBufferedLargeRpcBytes(client).Should().Be(0);
        GetPendingCalls(client).Should().BeEmpty();
        GetActiveStreams(client).Should().BeEmpty();
        GetCollectionCount(client, "_largeRpcCollectors").Should().Be(0);
    }

    private static long GetBufferedLargeRpcBytes(BoltClient client) =>
        (long)GetField(client, "_bufferedLargeRpcBytes");

    private static int GetCollectionCount(object target, string fieldName)
    {
        var collection = GetField(target, fieldName);
        return (int)(collection.GetType().GetProperty("Count")?.GetValue(collection)
            ?? throw new InvalidOperationException($"{fieldName} does not expose Count."));
    }

    private static async Task SendPayloadAndCloseAsync(
        BoltClient client,
        BoltStream stream,
        ReadOnlyMemory<byte> payload)
    {
        await InvokePipelineAsync(client, stream, payload);
        await stream.CloseAsync();
    }

    private static Task InvokePipelineAsync(
        BoltClient client,
        BoltStream stream,
        ReadOnlyMemory<byte> payload) =>
        (Task)(typeof(BoltClient)
            .GetMethod("SendLargePayloadPipelinedAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(client, [stream, payload, CancellationToken.None])
            ?? throw new InvalidOperationException("Large-RPC pipeline method was not found."));

    private static byte[] WriteFrame(Action<RentedBufferWriter> write)
    {
        using var writer = new RentedBufferWriter(128);
        write(writer);
        return writer.WrittenMemory.ToArray();
    }

    private static async ValueTask DispatchAsync(
        BoltClient client,
        BoltConnection connection,
        byte[] frame)
    {
        var dispatch = typeof(BoltClient).GetMethod(
            "DispatchReceivedFrameAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var operation = (ValueTask)dispatch.Invoke(
            client,
            [connection, frame, frame.Length, CancellationToken.None])!;
        await operation;
    }

    private static void InvokePrivate(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);

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

    private class CountingBoltConnection : IBoltConnection
    {
        private int _isConnectedReads;

        public int IsConnectedReads => Volatile.Read(ref _isConnectedReads);
        public bool SupportsDatagrams => false;
        public virtual bool IsConnected
        {
            get
            {
                Interlocked.Increment(ref _isConnectedReads);
                return true;
            }
        }
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public virtual ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default) => ValueTask.FromResult((0, true));

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CancellationIgnoringBoltConnection : CountingBoltConnection
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            SendStarted.TrySetResult();
            return new ValueTask(_release.Task);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class RecordingBoltConnection : CountingBoltConnection
    {
        public ConcurrentBag<HttpStatusCode> ResponseStatuses { get; } = [];

        public override ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (data.Span[0] == (byte)FrameType.Push &&
                BoltCodec.TryReadRequest(data.Span, out var frame, out _) &&
                frame.CommandHash == BoltCodec.Fnv1aHash("__bolt_large_rpc_response__"))
            {
                var payload = frame.GetPayload(data.Span);
                if (payload.Length >= 18)
                    ResponseStatuses.Add((HttpStatusCode)BinaryPrimitives.ReadInt16LittleEndian(payload[16..]));
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ControlledBoundaryBoltConnection : CountingBoltConnection
    {
        private int _completedWrites;

        public Channel<ControlledWrite> Writes { get; } = Channel.CreateUnbounded<ControlledWrite>();
        public int CompletedWrites => Volatile.Read(ref _completedWrites);

        public override ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            var write = new ControlledWrite(this, (FrameType)data.Span[0]);
            if (!Writes.Writer.TryWrite(write))
                throw new InvalidOperationException("Controlled write channel is closed.");
            return new ValueTask(write.Completion);
        }

        public sealed class ControlledWrite(
            ControlledBoundaryBoltConnection owner,
            FrameType frameType)
        {
            private readonly TaskCompletionSource _completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _completed;

            public FrameType FrameType { get; } = frameType;
            public Task Completion => _completion.Task;

            public void Complete()
            {
                if (Interlocked.Exchange(ref _completed, 1) != 0)
                    return;
                Interlocked.Increment(ref owner._completedWrites);
                _completion.TrySetResult();
            }
        }
    }
}
