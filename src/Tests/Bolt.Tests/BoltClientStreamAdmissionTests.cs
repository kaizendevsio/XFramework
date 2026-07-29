using System.Reflection;
using System.Collections.Concurrent;
using System.Net;
using Bolt.Client;
using Bolt.Protocol.Transport;
using FluentAssertions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltClientStreamAdmissionTests
{
    [Test]
    public void TryEnqueueInbound_WhenStreamBufferIsFull_FailsStreamWithoutWaiting()
    {
        var closed = 0;
        var stream = CreateStream(new BoltConnection(new RecordingBoltConnection()), 1, _ => closed++);
        var enqueue = typeof(BoltStream).GetMethod(
            "TryEnqueueInbound",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        ReadOnlyMemory<byte> first = new byte[4];
        ReadOnlyMemory<byte> overflow = new byte[4];

        ((bool)enqueue.Invoke(stream, [first])!).Should().BeTrue();
        ((bool)enqueue.Invoke(stream, [overflow])!).Should().BeFalse();

        stream.IsClosed.Should().BeTrue();
        closed.Should().Be(1);
    }

    [Test]
    public void RetireStreamsForConnection_ClosesOnlyOwnedStreamsAndReleasesAdmissionCount()
    {
        var firstConnection = new BoltConnection(new RecordingBoltConnection());
        var secondConnection = new BoltConnection(new RecordingBoltConnection());
        var client = new BoltClient(
            new Uri("ws://localhost:1/bolt"),
            "stream_client",
            "StreamClient",
            new BoltClientOptions { MaxActiveStreams = 2 },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BoltClient>.Instance);
        var first = CreateStream(firstConnection, 1, _ => { });
        var second = CreateStream(secondConnection, 1, _ => { });
        var track = typeof(BoltClient).GetMethod("TryTrackStream", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((bool)track.Invoke(client, [first])!).Should().BeTrue();
        ((bool)track.Invoke(client, [second])!).Should().BeTrue();

        typeof(BoltClient)
            .GetMethod("RetireStreamsForConnection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(client, [firstConnection, HttpStatusCode.ServiceUnavailable]);

        first.IsClosed.Should().BeTrue();
        second.IsClosed.Should().BeFalse();
        var streams = (ConcurrentDictionary<Guid, BoltStream>)typeof(BoltClient)
            .GetField("_activeStreams", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        streams.Keys.Should().ContainSingle().Which.Should().Be(second.StreamId);
        ((int)typeof(BoltClient)
            .GetField("_activeStreamCount", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!).Should().Be(1);
    }

    [Test]
    public async Task EarlyInboundCancellationState_IsBoundedAndClearedOnDispose()
    {
        var client = new BoltClient(
            new Uri("ws://localhost:1/bolt"),
            "cancel_client",
            "CancelClient",
            new BoltClientOptions
            {
                MaxConcurrentInboundHandlers = 1,
                MaxActiveStreams = 1
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BoltClient>.Instance);
        var track = typeof(BoltClient).GetMethod(
            "TrackEarlyInboundCancellation",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (var index = 0; index < 10; index++)
            track.Invoke(client, [Guid.NewGuid()]);

        var cancellations = (ConcurrentDictionary<Guid, long>)typeof(BoltClient)
            .GetField("_earlyInboundCancellations", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        cancellations.Count.Should().BeLessThanOrEqualTo(2);

        await client.DisposeAsync();

        cancellations.Should().BeEmpty();
    }

    private static BoltStream CreateStream(BoltConnection connection, int capacity, Action<Guid> onClosed)
    {
        var constructor = typeof(BoltStream).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(Guid), typeof(BoltConnection), typeof(Action<Guid>), typeof(int)],
            modifiers: null)!;
        return (BoltStream)constructor.Invoke([Guid.NewGuid(), connection, onClosed, capacity]);
    }

    private sealed class RecordingBoltConnection : IBoltConnection
    {
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default) => ValueTask.FromResult((0, true));
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
