using System.Diagnostics;
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
[CancelAfter(30_000)]
public sealed class BoltLargeRpcPipelineOwnershipTests
{
    private const int ChunkSize = 1024;

    [TestCase(2)]
    [TestCase(4)]
    [TestCase(8)]
    public async Task Pipeline_QueuesAtMostConfiguredByteWindowBeforePhysicalProgress(int pipelineChunks)
    {
        var transport = new ControlledWriteConnection();
        var connection = new BoltConnection(transport, enableBatching: false);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateClient(pipelineChunks);
        var stream = CreateStream(connection);
        var payloadChunks = pipelineChunks + 2;

        try
        {
            var operation = SendPayloadAndCloseAsync(
                client,
                stream,
                new byte[payloadChunks * ChunkSize],
                CancellationToken.None);

            await transport.WaitForWriteCountAsync(1);
            await WaitUntilAsync(() => connection.PendingSends == pipelineChunks);

            transport.WriteCount.Should().Be(1,
                "the next chunk must not be enqueued until the oldest physical-send completion advances");
            connection.PendingSends.Should().Be(pipelineChunks);

            for (var index = 0; index < payloadChunks; index++)
            {
                await transport.WaitForWriteCountAsync(index + 1);
                transport.GetFrameType(index).Should().Be(FrameType.StreamData);
                transport.CompleteWrite(index);
            }

            await transport.WaitForWriteCountAsync(payloadChunks + 1);
            transport.CompletedWriteCount.Should().Be(payloadChunks);
            transport.GetFrameType(payloadChunks).Should().Be(FrameType.StreamClose,
                "StreamClose must not reach the physical transport before all StreamData writes complete");

            transport.CompleteWrite(payloadChunks);
            await operation.WaitAsync(TimeSpan.FromSeconds(2));
            await WaitUntilAsync(() => connection.PendingSends == 0);
        }
        finally
        {
            await StopConnectionAsync(connection, transport);
        }
    }

    [TestCase(2)]
    [TestCase(4)]
    [TestCase(8)]
    public async Task PipelineCancellation_ReturnsPromptlyWhilePhysicalWritesDrainWithoutLeaks(int pipelineChunks)
    {
        var transport = new ControlledWriteConnection();
        var connection = new BoltConnection(transport, enableBatching: false);
        connection.StartSendLoop(CancellationToken.None);
        await using var client = CreateClient(pipelineChunks);
        var stream = CreateStream(connection);
        using var cancellation = new CancellationTokenSource();

        try
        {
            var operation = InvokePipelineAsync(
                client,
                stream,
                new byte[(pipelineChunks + 2) * ChunkSize],
                cancellation.Token);

            await transport.WaitForWriteCountAsync(1);
            await WaitUntilAsync(() => connection.PendingSends == pipelineChunks);

            var stopwatch = Stopwatch.StartNew();
            cancellation.Cancel();
            var completed = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(1)));
            stopwatch.Stop();

            completed.Should().BeSameAs(operation);
            Assert.ThrowsAsync<OperationCanceledException>(async () => await operation);
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
            connection.PendingSends.Should().Be(pipelineChunks,
                "caller cancellation must not cancel or release already-enqueued physical writes");

            transport.CompleteAllWritesAndFutureWrites();
            await WaitUntilAsync(() => connection.PendingSends == 0);
            connection.ActiveSends.Should().Be(0);
            transport.WriteCount.Should().Be(pipelineChunks);

            await InvokePipelineAsync(
                client,
                stream,
                new byte[2 * ChunkSize],
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
            await WaitUntilAsync(() => connection.PendingSends == 0);
            connection.SendLoop.Should().NotBeNull();
            connection.SendLoop!.IsFaulted.Should().BeFalse();
        }
        finally
        {
            await StopConnectionAsync(connection, transport);
        }
    }

    [Test]
    public async Task DirectInboundSink_DrainsQueuedChunksBeforeHandlingNewChunksDirectly()
    {
        var connection = new BoltConnection(new ControlledWriteConnection());
        var stream = CreateStream(connection);
        var collector = new InboundCollector();

        AcceptInbound(stream, new byte[] { 1, 2 }).Should().BeTrue();
        AcceptInbound(stream, new byte[] { 3, 4 }).Should().BeTrue();

        InstallInboundSink(stream, collector).Should().BeTrue();
        AcceptInbound(stream, new byte[] { 5, 6 }).Should().BeTrue();
        MarkClosed(stream, HttpStatusCode.OK);
        await WaitForCloseAsync(stream).WaitAsync(TimeSpan.FromSeconds(1));

        collector.Chunks.Select(Convert.ToHexString).Should().Equal("0102", "0304", "0506");
        connection.PendingSends.Should().Be(0);
    }

    [Test]
    public async Task DirectInboundSink_CloseBeforeInstallation_DrainsQueuedChunksAndStaysClosed()
    {
        var connection = new BoltConnection(new ControlledWriteConnection());
        var stream = CreateStream(connection);
        var collector = new InboundCollector();

        AcceptInbound(stream, new byte[] { 7, 8 }).Should().BeTrue();
        AcceptInbound(stream, new byte[] { 9, 10 }).Should().BeTrue();
        MarkClosed(stream, HttpStatusCode.RequestTimeout);

        InstallInboundSink(stream, collector).Should().BeTrue();
        await WaitForCloseAsync(stream).WaitAsync(TimeSpan.FromSeconds(1));

        collector.Chunks.Select(Convert.ToHexString).Should().Equal("0708", "090A");
        stream.IsClosed.Should().BeTrue();
        stream.CloseStatus.Should().Be(HttpStatusCode.RequestTimeout);
        AcceptInbound(stream, new byte[] { 11 }).Should().BeFalse();
        collector.Chunks.Should().HaveCount(2);
    }

    private static BoltClient CreateClient(int pipelineChunks = 8) =>
        new(
            new Uri("ws://localhost:1/bolt"),
            "pipeline-test",
            "Pipeline Test",
            new BoltClientOptions
            {
                StreamChunkSize = ChunkSize,
                MaxLargeRpcPipelineBytes = pipelineChunks * ChunkSize,
                EnableBatching = false
            },
            NullLogger<BoltClient>.Instance);

    private static BoltStream CreateStream(BoltConnection connection) =>
        (BoltStream)(Activator.CreateInstance(
            typeof(BoltStream),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [Guid.NewGuid(), connection, null, 16],
            culture: null) ?? throw new InvalidOperationException("BoltStream constructor was not found."));

    private static async Task SendPayloadAndCloseAsync(
        BoltClient client,
        BoltStream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        await InvokePipelineAsync(client, stream, payload, cancellationToken);
        await stream.CloseAsync(ct: cancellationToken);
    }

    private static Task InvokePipelineAsync(
        BoltClient client,
        BoltStream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken) =>
        (Task)(typeof(BoltClient)
            .GetMethod("SendLargePayloadPipelinedAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(client, [stream, payload, cancellationToken])
            ?? throw new InvalidOperationException("Large-RPC pipeline method was not found."));

    private static bool AcceptInbound(BoltStream stream, ReadOnlySpan<byte> data)
    {
        var method = typeof(BoltStream).GetMethod(
            "TryAcceptInbound",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BoltStream inbound method was not found.");
        var accept = (TryAcceptInboundDelegate)method.CreateDelegate(typeof(TryAcceptInboundDelegate));
        return accept(stream, data);
    }

    private static bool InstallInboundSink(BoltStream stream, InboundCollector collector)
    {
        var sinkType = typeof(BoltStream).GetNestedType("InboundSink", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BoltStream inbound sink type was not found.");
        var sinkMethod = typeof(InboundCollector).GetMethod(
            nameof(InboundCollector.Accept),
            BindingFlags.Instance | BindingFlags.Public)!;
        var sink = sinkMethod.CreateDelegate(sinkType, collector);
        var install = typeof(BoltStream).GetMethod(
            "TrySetInboundSink",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BoltStream sink installation method was not found.");
        return (bool)install.Invoke(stream, [sink])!;
    }

    private static void MarkClosed(BoltStream stream, HttpStatusCode statusCode)
    {
        var method = typeof(BoltStream).GetMethod(
            "MarkClosed",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BoltStream close method was not found.");
        var markClosed = (MarkClosedDelegate)method.CreateDelegate(typeof(MarkClosedDelegate));
        markClosed(stream, statusCode);
    }

    private static Task WaitForCloseAsync(BoltStream stream) =>
        (Task)(typeof(BoltStream)
            .GetMethod("WaitForCloseAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(stream, [CancellationToken.None])
            ?? throw new InvalidOperationException("BoltStream close wait method was not found."));

    private static async Task StopConnectionAsync(
        BoltConnection connection,
        ControlledWriteConnection transport)
    {
        transport.CompleteAllWritesAndFutureWrites();
        connection.CompleteSendChannel();
        if (connection.SendLoop is not null)
            await connection.SendLoop.WaitAsync(TimeSpan.FromSeconds(2));
        await transport.DisposeAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        condition().Should().BeTrue();
    }

    private delegate bool TryAcceptInboundDelegate(BoltStream stream, ReadOnlySpan<byte> data);
    private delegate void MarkClosedDelegate(BoltStream stream, HttpStatusCode statusCode);

    private sealed class InboundCollector
    {
        public List<byte[]> Chunks { get; } = [];

        public void Accept(ReadOnlySpan<byte> data) => Chunks.Add(data.ToArray());
    }

    private sealed class ControlledWriteConnection : IBoltConnection
    {
        private sealed record Write(byte[] Frame, TaskCompletionSource Completion);

        private readonly object _gate = new();
        private readonly List<Write> _writes = [];
        private readonly SemaphoreSlim _writeStarted = new(0);
        private bool _completeFutureWrites;

        public bool SupportsDatagrams => false;
        public bool IsConnected { get; private set; } = true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public int WriteCount
        {
            get { lock (_gate) return _writes.Count; }
        }

        public int CompletedWriteCount
        {
            get { lock (_gate) return _writes.Count(write => write.Completion.Task.IsCompleted); }
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            var write = new Write(
                data.ToArray(),
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            bool completeImmediately;
            lock (_gate)
            {
                _writes.Add(write);
                completeImmediately = _completeFutureWrites;
            }

            _writeStarted.Release();
            if (completeImmediately)
                write.Completion.TrySetResult();
            return new ValueTask(write.Completion.Task);
        }

        public FrameType GetFrameType(int index)
        {
            lock (_gate)
                return (FrameType)_writes[index].Frame[0];
        }

        public void CompleteWrite(int index)
        {
            Write write;
            lock (_gate)
                write = _writes[index];
            write.Completion.TrySetResult();
        }

        public void CompleteAllWritesAndFutureWrites()
        {
            Write[] writes;
            lock (_gate)
            {
                _completeFutureWrites = true;
                writes = [.. _writes];
            }

            foreach (var write in writes)
                write.Completion.TrySetResult();
        }

        public async Task WaitForWriteCountAsync(int expected)
        {
            while (WriteCount < expected)
                await _writeStarted.WaitAsync(TimeSpan.FromSeconds(2));
        }

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default) => ValueTask.FromResult((0, true));

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default)
        {
            IsConnected = false;
            CompleteAllWritesAndFutureWrites();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            CompleteAllWritesAndFutureWrites();
            _writeStarted.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
