using System.Collections.Concurrent;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Rtc;
using Bolt.Server;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// The relay's congestion reports to a sender while its receiver takes media on a real data channel (the bolt-rtc
/// sidecar on loopback, no TURN): a fast, clean path must not look limited.
/// </summary>
public sealed partial class BoltGroupCallLifecycleTests
{
    [Test]
    [CancelAfter(90_000)]
    public async Task Datagram_RealChannel_FastPath_IsNotReportedAsLimited()
    {
        var binary = RtcSidecarTests.RequireBinary();
        await using var sidecar = new RtcSidecar(new RtcSidecarOptions { ExecutablePath = binary }, NullLogger<RtcSidecar>.Instance);
        var ice = new FakeIceSource();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = new BoltMediaTransportOptions
        {
            Peers = new LoopbackFactory(sidecar), IceServers = ice, RelayOnly = false, RequestSpacingSeconds = 0,
        });
        typeof(BoltServer).GetProperty("AllowLoopback", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(f.Server, true);
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);

        var a = f.Peers["a"];
        await a.ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Request, new MediaTransportRequest(1, RtcDefaults.MaxMessageBytes)));
        var config = await AwaitTransport<MediaTransportConfig>(a, MediaTransportKind.Config, x => x.Unavailable is null);
        await using var phone = await sidecar.CreateAsync(RtcPeerRole.Offer, new RtcPeerOptions([], false, RtcDefaults.MaxMessageBytes, 0, AllowLoopback: true), CancellationToken.None);
        long received = 0;
        phone.Message += data => Interlocked.Add(ref received, data.Length);
        phone.LocalCandidate += c => a.Send(MediaTransportCodec.Encode(MediaTransportKind.Candidate, new MediaTransportCandidate(config.Session, c.Candidate, c.SdpMid, c.SdpMLineIndex)));
        var offer = await phone.CreateOfferAsync(false, CancellationToken.None);
        await a.ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Offer, new MediaTransportDescription(config.Session, offer)));
        var answer = await AwaitTransport<MediaTransportDescription>(a, MediaTransportKind.Answer, x => x.Session == config.Session);
        await phone.SetAnswerAsync(answer.Sdp, CancellationToken.None);
        var forwarded = 0;
        var deadline = Environment.TickCount64 + 20_000;
        while (Connection(f, "a").Datagram is null)
        {
            foreach (var frame in a.Sent.Skip(forwarded).ToArray())
            {
                forwarded++;
                if (MediaTransportCodec.TryRead(frame, out var kind, out var payload) && kind == MediaTransportKind.Candidate &&
                    MediaTransportCodec.Decode<MediaTransportCandidate>(payload) is { } candidate)
                    await phone.AddCandidateAsync(new RtcCandidate(candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex), CancellationToken.None);
            }
            if (Environment.TickCount64 > deadline) Assert.Fail("the channel did not open");
            await Task.Delay(10);
        }

        // b sends 1.2 Mbit/s of video (single-message pictures) and 40 kbit/s of audio for 8 s.
        var audio = await f.Config("b");
        var video = await f.VideoConfig("b");
        var started = Environment.TickCount64;
        var tick = 0;
        while (Environment.TickCount64 - started < 8_000)
        {
            await f.Send("b", audio);
            for (var i = 0; i < 3; i++) await f.SendVideo("b", video, keyframe: tick % 150 == 0 && i == 0, bytes: 1000);
            tick++;
            var due = started + tick * 20;
            var wait = due - Environment.TickCount64;
            if (wait > 0) await Task.Delay((int)wait);
        }
        var reports = f.Peers["b"].Congestion(video);
        var hub = Connection(f, "a");
        TestContext.Out.WriteLine($"received={Interlocked.Read(ref received) * 8 / 8000} kbps datagram={hub.DatagramFramesSent} fallbacks={hub.DatagramFallbacks} socketMedia={a.Count(FrameType.MediaFrame)}");
        foreach (var r in reports.TakeLast(12))
            TestContext.Out.WriteLine($"queue={r.QueueDelayMs} uplink={r.UplinkDelayMs} allowed={r.AllowedKbps} flags={r.Flags} dropped={r.DroppedPictures}");
        Assert.Multiple(() =>
        {
            Assert.That(reports.Skip(8).Count(x => (x.Flags & MediaCongestionFlags.Limited) != 0), Is.Zero, "a fast receiver path is not a limit");
            Assert.That(reports.Skip(8).Max(x => x.QueueDelayMs), Is.LessThan(100));
        });
    }

    private sealed class LoopbackFactory(RtcSidecar sidecar) : IRtcPeerFactory
    {
        public ValueTask<IRtcPeer> CreateAsync(RtcPeerRole role, RtcPeerOptions options, CancellationToken ct) =>
            sidecar.CreateAsync(role, options with { IceServers = [], AllowLoopback = true, RelayOnly = false }, ct);
    }
}
