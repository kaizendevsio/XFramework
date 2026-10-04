using Bolt.Client;
using Bolt.Media;
using Bolt.Protocol;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>Sender side of selective retransmission: what a NACK the relay forwarded gets back, and where it goes.</summary>
public sealed class MediaRetransmissionTests
{
    [Test]
    public async Task ANackForwardedByTheRelay_GetsTheFrameAgain_OnTheDataChannelOnly()
    {
        var connection = new BoltConnection(new BoltSFrameInteropTestsNoop());
        await using var stream = new BoltMediaStream(connection, Guid.NewGuid(), Guid.NewGuid(), isAudio: false);
        var resent = new List<byte[]>();
        var channelOpen = true;
        stream.EnableRetransmission(frame => { if (!channelOpen) return false; resent.Add(frame.ToArray()); return true; });
        await stream.SendPictureAsync([new byte[300], new byte[300], new byte[120]], isKeyframe: true, timestamp: 9000);

        await stream.HandleNackAsync([1, 7]);
        Assert.That(resent, Has.Count.EqualTo(1), "7 was never sent");
        Assert.That(BoltCodec.TryReadMediaFrame(resent[0], out var header), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(header.SequenceNumber, Is.EqualTo(1u));
            Assert.That(header.Timestamp, Is.EqualTo(9000u), "byte for byte the frame it was: same sequence, timestamp and ciphertext");
            Assert.That(stream.RetransmittedFrames, Is.EqualTo(1));
        });

        channelOpen = false;
        await stream.HandleNackAsync([0, 2]);
        Assert.That(resent, Has.Count.EqualTo(1), "no channel: nothing is resent on the WebSocket");
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task WithoutRetransmission_ANackOverTheWebSocketIsIgnored_AsBefore()
    {
        var connection = new BoltConnection(new BoltSFrameInteropTestsNoop());
        await using var stream = new BoltMediaStream(connection, Guid.NewGuid(), Guid.NewGuid(), isAudio: false);
        stream.EnableNack();
        await stream.SendPictureAsync([new byte[300]], isKeyframe: true, timestamp: 9000);
        await stream.HandleNackAsync([0]);
        Assert.That(stream.RetransmittedFrames, Is.Zero, "TCP does not lose frames; a gap there is a relay's deliberate drop");
        connection.CompleteSendChannel();
    }

    private sealed class BoltSFrameInteropTestsNoop : Bolt.Protocol.Transport.IBoltConnection
    {
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public Bolt.Protocol.Transport.BoltTransport TransportType => Bolt.Protocol.Transport.BoltTransport.WebSocket;
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
            => ValueTask.FromResult((0, true));
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
