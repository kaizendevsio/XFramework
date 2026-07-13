using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
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
public sealed class BoltServerSendFailureTests
{
    [TestCase(SendFailureMode.Blocked)]
    [TestCase(SendFailureMode.Faulted)]
    public async Task HandleConnectionAsync_TransportSendFailure_RetiresOnlyFailedConnectionAndCompletesPendingWork(
        SendFailureMode failureMode)
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions
            {
                SendEnqueueTimeoutMs = 75,
                TransportCloseTimeoutMs = 250
            });
        await using var caller = new TestBoltConnection();
        await using var failedRecipient = new TestBoltConnection(failureMode, successfulSendsBeforeFailure: 2);
        await using var healthyRecipient = new TestBoltConnection();
        var callerTask = server.HandleConnectionAsync(caller, CancellationToken.None);
        var failedRecipientTask = server.HandleConnectionAsync(failedRecipient, CancellationToken.None);
        var healthyRecipientTask = server.HandleConnectionAsync(healthyRecipient, CancellationToken.None);

        try
        {
            caller.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "send-failure-caller", "Caller")));
            failedRecipient.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "failing-recipient", "FailingRecipient")));
            healthyRecipient.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "healthy-recipient", "HealthyRecipient")));
            await Task.WhenAll(
                caller.WaitForSentFramesAsync(1),
                failedRecipient.WaitForSentFramesAsync(1),
                healthyRecipient.WaitForSentFramesAsync(1));

            var failedConnection = FindRegisteredConnection(server, "failing-recipient");
            var callerHash = BoltCodec.Fnv1aHash("send-failure-caller");
            var failedRecipientHash = BoltCodec.Fnv1aHash("failing-recipient");
            var streamId = Guid.NewGuid();
            caller.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamOpen(
                writer,
                streamId,
                failedRecipientHash,
                BoltCodec.Fnv1aHash("stream"))));
            await failedRecipient.WaitForSentFramesAsync(2);

            var failedRequestId = Guid.NewGuid();
            caller.Enqueue(WriteFrame(writer => BoltCodec.WriteRequest(
                writer,
                failedRequestId,
                failedRecipientHash,
                callerHash,
                BoltCodec.Fnv1aHash("fail"),
                ReadOnlySpan<byte>.Empty)));

            await caller.WaitForSentFramesAsync(3);
            await WaitForConditionAsync(() => server.ConnectedClients == 2 && failedRecipientTask.IsCompleted);

            var cleanupFrames = caller.SentFrames.ToArray().Skip(1).ToArray();
            cleanupFrames.Any(frame =>
                BoltCodec.TryReadResponse(frame, out var response, out _) &&
                response.RequestId == failedRequestId &&
                response.StatusCode == HttpStatusCode.ServiceUnavailable).Should().BeTrue();
            cleanupFrames.Any(frame =>
                BoltCodec.TryReadStreamClose(frame, out var closedStreamId, out var statusCode) &&
                closedStreamId == streamId &&
                statusCode == HttpStatusCode.ServiceUnavailable).Should().BeTrue();
            server.GetHealthSnapshot().PendingRpcCalls.Should().Be(0);
            server.GetHealthSnapshot().ActiveLogicalStreams.Should().Be(0);
            failedConnection.IsAlive.Should().BeFalse();
            failedConnection.SendLoop.Should().NotBeNull();
            failedConnection.SendLoop!.IsFaulted.Should().BeTrue();

            if (failureMode == SendFailureMode.Blocked)
            {
                failedConnection.TransportSendTimeoutCount.Should().Be(1);
                failedConnection.TransportSendFailureCount.Should().Be(0);
                failedConnection.SendFailure.Should().BeOfType<BoltTransportSendTimeoutException>();
            }
            else
            {
                failedConnection.TransportSendTimeoutCount.Should().Be(0);
                failedConnection.TransportSendFailureCount.Should().Be(1);
                failedConnection.SendFailure.Should().BeOfType<BoltTransportSendException>();
            }

            callerTask.IsCompleted.Should().BeFalse();
            healthyRecipientTask.IsCompleted.Should().BeFalse();
            caller.IsConnected.Should().BeTrue();
            healthyRecipient.IsConnected.Should().BeTrue();

            var healthyRequestId = Guid.NewGuid();
            caller.Enqueue(WriteFrame(writer => BoltCodec.WriteRequest(
                writer,
                healthyRequestId,
                BoltCodec.Fnv1aHash("healthy-recipient"),
                callerHash,
                BoltCodec.Fnv1aHash("healthy"),
                ReadOnlySpan<byte>.Empty)));
            await healthyRecipient.WaitForSentFramesAsync(2);
            healthyRecipient.Enqueue(WriteFrame(writer => BoltCodec.WriteResponse(
                writer,
                healthyRequestId,
                HttpStatusCode.OK,
                ReadOnlySpan<byte>.Empty)));
            await caller.WaitForSentFramesAsync(4);

            caller.SentFrames.Any(frame =>
                BoltCodec.TryReadResponse(frame, out var response, out _) &&
                response.RequestId == healthyRequestId &&
                response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        }
        finally
        {
            caller.Complete();
            failedRecipient.Complete();
            healthyRecipient.Complete();
            await Task.WhenAll(callerTask, failedRecipientTask, healthyRecipientTask)
                .WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    private static BoltHubConnection FindRegisteredConnection(BoltServer server, string clientId) =>
        ((ConcurrentDictionary<string, BoltHubConnection>)(typeof(BoltServer)
            .GetField("_connectionsByStreamId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(server) ?? throw new MissingFieldException(typeof(BoltServer).FullName, "_connectionsByStreamId")))
        .Values.Single(connection => connection.ClientId == clientId);

    private static byte[] WriteFrame(Func<IBufferWriter<byte>, int> write)
    {
        var writer = new ArrayBufferWriter<byte>();
        write(writer);
        return writer.WrittenSpan.ToArray();
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met before timeout.");
    }

    public enum SendFailureMode
    {
        Blocked,
        Faulted
    }

    private sealed class TestBoltConnection(
        SendFailureMode? failureMode = null,
        int successfulSendsBeforeFailure = int.MaxValue) : IBoltConnection
    {
        private readonly Channel<byte[]> _incoming = Channel.CreateUnbounded<byte[]>();
        private int _sendCount;
        private int _isConnected = 1;

        public ConcurrentQueue<byte[]> SentFrames { get; } = new();
        public bool SupportsDatagrams => false;
        public bool IsConnected => Volatile.Read(ref _isConnected) != 0;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public void Enqueue(byte[] frame) =>
            _incoming.Writer.TryWrite(frame).Should().BeTrue();

        public void Complete() => _incoming.Writer.TryComplete();

        public async Task WaitForSentFramesAsync(int expected)
        {
            await WaitForConditionAsync(() => SentFrames.Count >= expected);
        }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _sendCount) > successfulSendsBeforeFailure)
            {
                if (failureMode == SendFailureMode.Blocked)
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                throw new IOException("Synthetic transport send failure.");
            }

            SentFrames.Enqueue(data.ToArray());
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

            Interlocked.Exchange(ref _isConnected, 0);
            return (0, true);
        }

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default)
        {
            Interlocked.Exchange(ref _isConnected, 0);
            Complete();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _isConnected, 0);
            Complete();
            return ValueTask.CompletedTask;
        }
    }
}
