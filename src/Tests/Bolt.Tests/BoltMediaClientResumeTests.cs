using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using Bolt.Client;
using Bolt.Media;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// The client half of a resumable call: a transport that drops must not tear the call down by
/// itself when its host resumes calls, and the heartbeat a phone times its link with round-trips.
/// </summary>
public sealed class BoltMediaClientResumeTests
{
    [Test]
    public async Task LostTransport_EndsCallsByDefault_ButLeavesThemToAResumingHost()
    {
        foreach (var endOnDisconnect in new[] { true, false })
        {
            await using var client = ClientWith(out var connection, out _);
            await using var media = new BoltMediaClient(client, NullLogger<BoltMediaClient>.Instance) { EndCallsOnDisconnect = endOnDisconnect };
            var call = Guid.NewGuid();
            await media.JoinHostedGroupAsync(call);
            var stream = new BoltMediaStream(connection, Guid.NewGuid(), call, true);
            Assert.That(media.RegisterMediaStream(stream), Is.True);
            var ended = 0;
            media.OnCallEnded += _ => { Interlocked.Increment(ref ended); return Task.CompletedTask; };

            RaiseDisconnected(client);
            await Task.Delay(200);

            Assert.Multiple(() =>
            {
                Assert.That(ended, Is.EqualTo(endOnDisconnect ? 1 : 0),
                    endOnDisconnect ? "a plain client still ends its calls with the transport" : "a resuming host keeps its call through the drop");
                Assert.That(media.GetMediaStream(stream.StreamId) is null, Is.EqualTo(endOnDisconnect));
            });
            connection.CompleteSendChannel();
        }
    }

    [Test]
    public async Task Heartbeat_IsSentAsAnEightByteStamp_AndItsEchoIsReported()
    {
        await using var client = ClientWith(out var connection, out var transport);
        await using var media = new BoltMediaClient(client, NullLogger<BoltMediaClient>.Instance);
        var call = Guid.NewGuid();
        var echoes = new ConcurrentQueue<(Guid, long)>();
        media.OnHeartbeat += (id, stamp) => echoes.Enqueue((id, stamp));

        await media.SendHeartbeatAsync(call, 424242);
        Assert.That(() => transport.Sent.Count, Is.EqualTo(1).After(2000, 10));
        var sent = transport.Sent.Single();
        Assert.That(BoltCodec.TryReadCallSignal(sent, out var header), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(header.SignalType, Is.EqualTo(SignalType.Heartbeat));
            Assert.That(header.CallId, Is.EqualTo(call));
            Assert.That(BinaryPrimitives.ReadInt64LittleEndian(sent.AsSpan(header.PayloadOffset, header.PayloadLength)), Is.EqualTo(424242));
        });

        var before = media.LastInboundTick;
        await Task.Delay(20);
        Deliver(media, connection, "HandleCallSignal", sent);
        Assert.Multiple(() =>
        {
            Assert.That(echoes, Is.EqualTo(new[] { (call, 424242L) }));
            Assert.That(media.LastInboundTick, Is.GreaterThan(before), "an echo is inbound traffic");
        });
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task AnyRelayFrame_CountsAsInboundTraffic()
    {
        await using var client = ClientWith(out var connection, out _);
        await using var media = new BoltMediaClient(client, NullLogger<BoltMediaClient>.Instance);
        var writer = new ArrayBufferWriter<byte>();
        BoltCodec.WriteMediaFrame(writer, Guid.NewGuid(), 1, 960, 0, [1, 2, 3]);
        var before = media.LastInboundTick;
        await Task.Delay(20);
        Deliver(media, connection, "HandleMediaFrame", writer.WrittenSpan.ToArray());
        Assert.That(media.LastInboundTick, Is.GreaterThan(before), "a link busy with pictures is alive even while an echo waits behind them");
        connection.CompleteSendChannel();
    }

    private static void Deliver(BoltMediaClient media, BoltConnection connection, string handler, byte[] frame) =>
        typeof(BoltMediaClient).GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(media, [connection, frame, frame.Length]);

    private static void RaiseDisconnected(BoltClient client) =>
        ((Action?)typeof(BoltClient).GetField("Disconnected", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client))!.Invoke();

    private static BoltClient ClientWith(out BoltConnection connection, out RecordingTransport transport)
    {
        var client = new BoltClient(new Uri("wss://localhost/bolt"), "resume-test", "Resume Test", new BoltClientOptions(), NullLogger<BoltClient>.Instance);
        transport = new RecordingTransport();
        connection = new BoltConnection(transport);
        connection.StartSendLoop(CancellationToken.None);
        ((List<BoltConnection>)typeof(BoltClient).GetField("_connections", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!).Add(connection);
        return client;
    }

    private sealed class RecordingTransport : IBoltConnection
    {
        public ConcurrentQueue<byte[]> Sent { get; } = new();
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) { Sent.Enqueue(data.ToArray()); return ValueTask.CompletedTask; }
        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
        { await Task.Delay(Timeout.Infinite, ct); return (0, true); }
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
