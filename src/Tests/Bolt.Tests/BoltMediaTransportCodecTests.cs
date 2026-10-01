using System.Buffers;
using Bolt.Media.Congestion;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>Phase 3 wire pieces: transport signalling frames, redundancy bundles, the duplicate window, the datagram policy.</summary>
public sealed class BoltMediaTransportCodecTests
{
    [Test]
    public void SignallingFrames_RoundTrip_AndAreBounded()
    {
        var config = new MediaTransportConfig("abc", [new RtcIceServer(["turn:t.example:3478?transport=udp"], "u", "p")], "all", 1150, 1_900_000_000);
        var frame = MediaTransportCodec.Encode(MediaTransportKind.Config, config);
        Assert.That(MediaTransportCodec.TryRead(frame, out var kind, out var payload), Is.True);
        var decoded = MediaTransportCodec.Decode<MediaTransportConfig>(payload)!;
        Assert.Multiple(() =>
        {
            Assert.That(kind, Is.EqualTo(MediaTransportKind.Config));
            Assert.That(decoded.Session, Is.EqualTo("abc"));
            Assert.That(decoded.IceServers.Single().Urls, Is.EqualTo(config.IceServers[0].Urls));
            Assert.That(decoded.IceServers.Single().Credential, Is.EqualTo("p"));
            Assert.That(decoded.MaxMessageBytes, Is.EqualTo(1150));
            Assert.That(decoded.Unavailable, Is.Null);
        });

        var wrongVersion = frame.ToArray(); wrongVersion[1] = 2;
        var wrongKind = frame.ToArray(); wrongKind[2] = 0x7F;
        Assert.Multiple(() =>
        {
            Assert.That(MediaTransportCodec.TryRead(wrongVersion, out _, out _), Is.False);
            Assert.That(MediaTransportCodec.TryRead(wrongKind, out _, out _), Is.False);
            Assert.That(MediaTransportCodec.TryRead(frame.AsSpan(0, frame.Length - 1), out _, out _), Is.False, "truncated");
            Assert.That(MediaTransportCodec.TryRead([.. frame, 0], out _, out _), Is.False, "trailing bytes");
            Assert.That(() => MediaTransportCodec.Write(new ArrayBufferWriter<byte>(), MediaTransportKind.Offer, new byte[MediaTransportCodec.MaxPayloadBytes + 1]),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(MediaTransportCodec.Decode<MediaTransportConfig>("not json"u8), Is.Null);
        });
    }

    [Test]
    public void IceServers_NeverPrintTheirCredential()
    {
        var server = new RtcIceServer(["turns:t.example:443?transport=tcp"], "user", "very-secret");
        Assert.That(server.ToString(), Does.Not.Contain("very-secret").And.Not.Contain("user"));
    }

    [Test]
    public void Bundles_CarryMediaFramesOnly_OldestFirst()
    {
        var first = Media(1); var second = Media(2);
        var bundle = new byte[MediaBundleCodec.Size(first.Length, second.Length)];
        MediaBundleCodec.Write(bundle, first, second);
        var parts = new Range[MediaBundleCodec.MaxFrames];
        Assert.That(MediaBundleCodec.TryRead(bundle, parts, out var count), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(2));
            Assert.That(bundle.AsSpan()[parts[0]].ToArray(), Is.EqualTo(first));
            Assert.That(bundle.AsSpan()[parts[1]].ToArray(), Is.EqualTo(second));
        });

        var signal = Write(w => BoltCodec.WriteCallSignal(w, Guid.NewGuid(), SignalType.End, new byte[16]));
        var smuggled = new byte[MediaBundleCodec.Size(first.Length, signal.Length)];
        MediaBundleCodec.Write(smuggled, first, signal);
        var empty = new byte[] { (byte)FrameType.MediaBundle, 0 };
        var tooMany = (byte[])bundle.Clone(); tooMany[1] = 5;
        Assert.Multiple(() =>
        {
            Assert.That(MediaBundleCodec.TryRead(smuggled, parts, out _), Is.False, "only media frames may ride a bundle");
            Assert.That(MediaBundleCodec.TryRead(bundle.AsSpan(0, bundle.Length - 1), parts, out _), Is.False);
            Assert.That(MediaBundleCodec.TryRead([.. bundle, 0], parts, out _), Is.False);
            Assert.That(MediaBundleCodec.TryRead(empty, parts, out _), Is.False);
            Assert.That(MediaBundleCodec.TryRead(tooMany, parts, out _), Is.False);
        });
    }

    [Test]
    public void SequenceWindow_TakesEachFrameOnce_InAnyOrder()
    {
        var window = new SequenceWindow();
        Assert.Multiple(() =>
        {
            Assert.That(window.TryMark(100), Is.True);
            Assert.That(window.TryMark(100), Is.False, "a duplicate");
            Assert.That(window.TryMark(103), Is.True);
            Assert.That(window.TryMark(101), Is.True, "late but unseen");
            Assert.That(window.TryMark(101), Is.False);
            Assert.That(window.TryMark(102), Is.True);
            Assert.That(window.TryMark(unchecked((uint)(103 - SequenceWindow.Size + 1))), Is.True, "the oldest slot still in the window");
        });
        var restart = new SequenceWindow();
        restart.TryMark(50_000);
        Assert.Multiple(() =>
        {
            Assert.That(restart.TryMark(7), Is.True, "far behind is a sender that started over");
            Assert.That(restart.TryMark(7), Is.False);
            Assert.That(restart.TryMark(8), Is.True);
        });
    }

    [Test]
    public void SequenceWindow_MeasuresLoss_PerInterval()
    {
        var window = new SequenceWindow();
        foreach (var sequence in new uint[] { 1, 2, 4, 5, 6, 8, 9, 10 }) window.TryMark(sequence);
        var (expected, received) = window.Sample();
        Assert.Multiple(() =>
        {
            Assert.That(expected, Is.EqualTo(10));
            Assert.That(received, Is.EqualTo(8));
            Assert.That(LossMath.Permille(expected, received), Is.EqualTo(200));
            Assert.That(window.Sample(), Is.EqualTo((0L, 0L)), "nothing since");
            Assert.That(LossMath.Permille(0, 0), Is.Zero);
        });
        window.TryMark(3); // A late frame of the last interval counts in this one.
        window.TryMark(11);
        Assert.That(window.Sample(), Is.EqualTo((1L, 1L)));
    }

    [Test]
    public void DatagramPolicy_KeepsStateChangesOnTheAuthenticatedSocket()
    {
        foreach (var type in new[] { FrameType.MediaConfig, FrameType.CallSignal, FrameType.Register, FrameType.MediaTransport, FrameType.Request, FrameType.MediaCongestion, FrameType.NackRequest })
            Assert.That(DatagramFramePolicy.AcceptFromParticipant(type), Is.False, type.ToString());
        foreach (var type in new[] { FrameType.MediaFrame, FrameType.FecFrame, FrameType.MediaFeedback, FrameType.MediaKeyRequest, FrameType.MediaBundle })
            Assert.That(DatagramFramePolicy.AcceptFromParticipant(type), Is.True, type.ToString());
        foreach (var type in new[] { FrameType.MediaConfig, FrameType.CallSignal, FrameType.MediaTransport, FrameType.RegisterAck, FrameType.Response })
            Assert.That(DatagramFramePolicy.AcceptFromRelay(type), Is.False, type.ToString());
        Assert.That(DatagramFramePolicy.AcceptFromRelay(FrameType.MediaCongestion), Is.True, "the relay's reports may take the media path");
    }

    [Test]
    public void Paths_DescribeTheLegThisSideControls()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new RtcPath("relay", "udp", "udp", "srflx", 80).Describe(), Is.EqualTo("UDP/relay"));
            Assert.That(new RtcPath("relay", "udp", "tls", "relay", 80).Describe(), Is.EqualTo("TLS/relay"));
            Assert.That(new RtcPath("relay", "udp", "tls", "relay", 80).IsStreamBased, Is.True);
            Assert.That(new RtcPath("srflx", "udp", null, "relay", 80).Describe(), Is.EqualTo("UDP/srflx"));
            Assert.That(new RtcPath("srflx", "udp", null, "relay", 80).IsStreamBased, Is.False);
        });
    }

    [Test]
    public async Task Pacer_TellsTheTransportWhichFramesAreAudio()
    {
        var seen = new List<(byte Marker, bool Audio)>();
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var pacer = new MediaSendPacer((frame, audio, _) =>
        {
            lock (seen) { seen.Add((frame.Span[0], audio)); if (seen.Count == 3) sent.TrySetResult(); }
            return ValueTask.CompletedTask;
        });
        pacer.ExpectKeyframe();
        pacer.EnqueueVideo(new PacedPicture([[2], [3]], Keyframe: true));
        pacer.EnqueueAudio([1]);
        pacer.Start();
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        lock (seen) Assert.That(seen, Is.EquivalentTo(new[] { ((byte)1, true), ((byte)2, false), ((byte)3, false) }));
    }

    private static byte[] Media(uint sequence) => Write(w => BoltCodec.WriteMediaFrame(w, Guid.NewGuid(), sequence, 960 * sequence, 0x10, [1, 2, 3]));

    private static byte[] Write(Action<IBufferWriter<byte>> write)
    {
        var writer = new ArrayBufferWriter<byte>();
        write(writer);
        return writer.WrittenSpan.ToArray();
    }
}
