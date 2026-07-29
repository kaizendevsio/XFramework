using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(15000)]
public sealed class BoltServerBatchingTests
{
    [Test]
    public async Task SendLoop_BatchesQueuedControlFrames_ButPreservesMediaOrdering()
    {
        await using var transport = new RecordingConnection();
        var connection = new BoltHubConnection(transport, sendQueueCapacity: 8, sendEnqueueTimeoutMs: 1_000);

        await connection.SendAsync(WriteRequestCancel(), CancellationToken.None);
        await connection.SendAsync(WriteRequestCancel(), CancellationToken.None);
        await connection.SendAsync(new byte[] { (byte)FrameType.MediaFrame }, CancellationToken.None);
        await connection.SendAsync(WriteRequestCancel(), CancellationToken.None);

        connection.StartSendLoop(CancellationToken.None);
        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(3));

        transport.Sent.Should().HaveCount(3);
        transport.Sent[0][0].Should().Be((byte)FrameType.Batch);
        BoltCodec.TryReadBatch(transport.Sent[0], out var batch).Should().BeTrue();
        batch.Count.Should().Be(2);
        transport.Sent[1][0].Should().Be((byte)FrameType.MediaFrame);
        transport.Sent[2][0].Should().Be((byte)FrameType.RequestCancel);
        connection.PendingBytes.Should().Be(0);
    }

    [Test]
    public async Task Registration_WithMismatchedWireVersion_ReturnsVersionedRejection()
    {
        using var server = new BoltServer(NullLogger<BoltServer>.Instance, new BoltServerOptions());
        await using var transport = new DuplexConnection();
        var serverTask = server.HandleConnectionAsync(transport, CancellationToken.None);
        var register = new ArrayBufferWriter<byte>();
        BoltCodec.WriteRegister(register, "old-client", "OldClient");
        var frame = register.WrittenSpan.ToArray();
        frame[1] = 1;
        frame[2] = 0;

        transport.Enqueue(frame);
        await transport.SentSignal.Task.WaitAsync(TimeSpan.FromSeconds(3));

        transport.Sent.Should().ContainSingle();
        BoltCodec.TryReadRegisterAck(transport.Sent[0], out var success, out var version).Should().BeTrue();
        success.Should().BeFalse();
        version.Should().Be(BoltCodec.WireVersion);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task Registration_WithActualWireV1Layout_ReturnsVersionedRejectionAndCloses()
    {
        using var server = new BoltServer(NullLogger<BoltServer>.Instance, new BoltServerOptions());
        await using var transport = new DuplexConnection();
        var serverTask = server.HandleConnectionAsync(transport, CancellationToken.None);

        transport.Enqueue(WriteWireV1Register("old-client", "OldClient"));
        await transport.SentSignal.Task.WaitAsync(TimeSpan.FromSeconds(3));

        transport.Sent.Should().ContainSingle();
        BoltCodec.TryReadRegisterAck(transport.Sent[0], out var success, out var version).Should().BeTrue();
        success.Should().BeFalse();
        version.Should().Be(BoltCodec.WireVersion);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));
        transport.IsConnected.Should().BeFalse();
    }

    [Test]
    public async Task SendLoop_BatchedReliableSendTimesOut_WhenTransportIgnoresCancellation()
    {
        await using var transport = new ControlledSendConnection(SendBehavior.Blocked);
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 8,
            sendEnqueueTimeoutMs: 50);
        var sends = QueueReliableBatch(connection);

        connection.PendingBytes.Should().BeGreaterThan(0);
        connection.StartSendLoop(CancellationToken.None);
        await transport.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var failures = await Task.WhenAll(sends.Select(CaptureFailureAsync));
        failures.Should().AllSatisfy(failure =>
            failure.Should().BeOfType<BoltTransportSendTimeoutException>());
        await FluentActions.Awaiting(async () => await connection.SendLoop!)
            .Should().ThrowAsync<BoltTransportSendTimeoutException>();

        transport.Sent.Should().ContainSingle();
        transport.Sent[0][0].Should().Be((byte)FrameType.Batch);
        connection.PendingBytes.Should().Be(0);
        connection.TransportSendTimeoutCount.Should().Be(1);
        transport.Release();
    }

    [Test]
    public async Task SendLoop_BatchedReliableSendFails_WhenTransportFaults()
    {
        await using var transport = new ControlledSendConnection(SendBehavior.Faulted);
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 8,
            sendEnqueueTimeoutMs: 1_000);
        var sends = QueueReliableBatch(connection);

        connection.StartSendLoop(CancellationToken.None);
        await transport.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var failures = await Task.WhenAll(sends.Select(CaptureFailureAsync));
        failures.Should().AllSatisfy(failure =>
            failure.Should().BeOfType<BoltTransportSendException>());
        await FluentActions.Awaiting(async () => await connection.SendLoop!)
            .Should().ThrowAsync<BoltTransportSendException>();

        transport.Sent.Should().ContainSingle();
        transport.Sent[0][0].Should().Be((byte)FrameType.Batch);
        connection.PendingBytes.Should().Be(0);
        connection.TransportSendFailureCount.Should().Be(1);
    }

    [Test]
    public async Task SendLoop_DisconnectDuringBatchedReliableSend_CancelsAllCompletionsAndClearsPendingBytes()
    {
        await using var transport = new ControlledSendConnection(SendBehavior.Blocked);
        var connection = new BoltHubConnection(
            transport,
            sendQueueCapacity: 8,
            sendEnqueueTimeoutMs: 1_000);
        using var loopCancellation = new CancellationTokenSource();
        var sends = QueueReliableBatch(connection);

        connection.StartSendLoop(loopCancellation.Token);
        await transport.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        transport.Disconnect();
        loopCancellation.Cancel();
        connection.CompleteSendChannel();

        var failures = await Task.WhenAll(sends.Select(CaptureFailureAsync));
        failures.Should().AllSatisfy(failure =>
            failure.Should().BeAssignableTo<OperationCanceledException>());
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(3));

        transport.Sent.Should().ContainSingle();
        transport.Sent[0][0].Should().Be((byte)FrameType.Batch);
        connection.PendingBytes.Should().Be(0);
        connection.SendFailure.Should().BeNull();
        transport.Release();
    }

    private static byte[] WriteRequestCancel()
    {
        var writer = new ArrayBufferWriter<byte>();
        BoltCodec.WriteRequestCancel(writer, Guid.NewGuid());
        return writer.WrittenSpan.ToArray();
    }

    private static byte[] WriteWireV1Register(string clientId, string clientName)
    {
        var id = Encoding.UTF8.GetBytes(clientId);
        var name = Encoding.UTF8.GetBytes(clientName);
        var frame = new byte[9 + id.Length + name.Length];
        frame[0] = (byte)FrameType.Register;
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(1), id.Length);
        id.CopyTo(frame.AsSpan(5));
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(5 + id.Length), name.Length);
        name.CopyTo(frame.AsSpan(9 + id.Length));
        return frame;
    }

    private static Task[] QueueReliableBatch(BoltHubConnection connection) =>
    [
        InvokeReliableSend(connection),
        InvokeReliableSend(connection)
    ];

    private static Task InvokeReliableSend(BoltHubConnection connection)
    {
        var method = typeof(BoltHubConnection).GetMethod(
            "SendAndCloseAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var send = (ValueTask)method.Invoke(
            connection,
            [new ReadOnlyMemory<byte>(WriteRequestCancel()), CancellationToken.None])!;
        return send.AsTask();
    }

    private static async Task<Exception> CaptureFailureAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new AssertionException("The reliable send completed successfully when a failure was expected.");
    }

    private enum SendBehavior
    {
        Blocked,
        Faulted
    }

    private sealed class ControlledSendConnection(SendBehavior behavior) : IBoltConnection
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _isConnected = 1;

        public List<byte[]> Sent { get; } = [];
        public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool SupportsDatagrams => false;
        public bool IsConnected => Volatile.Read(ref _isConnected) != 0;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Sent.Add(data.ToArray());
            SendStarted.TrySetResult();
            return behavior == SendBehavior.Faulted
                ? ValueTask.FromException(new IOException("Synthetic transport failure."))
                : new ValueTask(_release.Task);
        }

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default) => ValueTask.FromResult((0, true));

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default)
        {
            Disconnect();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disconnect();
            Release();
            return ValueTask.CompletedTask;
        }

        public void Disconnect() => Interlocked.Exchange(ref _isConnected, 0);

        public void Release() => _release.TrySetResult();
    }

    private sealed class RecordingConnection : IBoltConnection
    {
        public List<byte[]> Sent { get; } = [];
        public bool SupportsDatagrams => false;
        public bool IsConnected { get; private set; } = true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Sent.Add(data.ToArray());
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
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DuplexConnection : IBoltConnection
    {
        private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();
        public List<byte[]> Sent { get; } = [];
        public TaskCompletionSource SentSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool SupportsDatagrams => false;
        public bool IsConnected { get; private set; } = true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public void Enqueue(byte[] frame) => _inbound.Writer.TryWrite(frame);

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Sent.Add(data.ToArray());
            SentSignal.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default)
        {
            try
            {
                var frame = await _inbound.Reader.ReadAsync(ct);
                frame.CopyTo(buffer);
                return (frame.Length, true);
            }
            catch (ChannelClosedException)
            {
                return (0, true);
            }
        }

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default)
        {
            IsConnected = false;
            _inbound.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            _inbound.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
