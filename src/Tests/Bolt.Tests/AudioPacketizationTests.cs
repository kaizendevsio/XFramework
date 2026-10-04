using Bolt.Media.Congestion;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// Opus packet length follows the link: 20 ms where the link has room (latency first), 60 ms where it is scarce,
/// because every packet costs about 140 bytes of framing below the media on a datagram path (IP, UDP, TURN, DTLS,
/// SCTP, Bolt, SFrame). At 50 packets a second that is about 55 kbps; at about 17 a second, 18 kbps.
/// </summary>
public sealed class AudioPacketizationTests
{
    [Test]
    public void AScarceLink_GetsLongPackets_AfterAHold_AndARoomyOneShortPacketsAgain()
    {
        var audio = new AudioPacketization { MaxFrameMs = 60 };
        Assert.That(audio.Update(0, videoSuspended: false, 0), Is.Null, "no known limit keeps 20 ms");
        Assert.That(audio.Update(2_500, false, 500), Is.Null, "a link congesting at 2.5 Mbit/s has room");
        Assert.That(audio.Update(450, false, 1_000), Is.Null, "one congestion point on a 512 kbps link is not a regime yet");
        Assert.That(audio.Update(450, false, 1_500), Is.Null);
        Assert.That(audio.Update(450, false, 2_100), Is.EqualTo(60), "a second of it is");
        Assert.That(audio.FrameMs, Is.EqualTo(60));
        // The controller probes above the congestion point and forgets it (0): the link has not grown for that.
        Assert.That(audio.Update(0, false, 3_000), Is.Null, "a congestion point is remembered for 30 s");
        Assert.That(audio.Update(0, false, 32_200), Is.Null, "then the way back waits 5 s more");
        Assert.That(audio.Update(0, false, 36_000), Is.Null);
        Assert.That(audio.Update(0, false, 37_300), Is.EqualTo(20));
    }

    [Test]
    public void AnAudioOnlyCallOnAGoodLink_KeepsShortPackets()
    {
        // The camera is off: the estimate stays near what audio sends, but the link never congested, so it has room.
        var audio = new AudioPacketization { MaxFrameMs = 60 };
        for (var t = 0; t < 30_000; t += 250)
            Assert.That(audio.Update(0, videoSuspended: false, t), Is.Null);
        Assert.That(audio.FrameMs, Is.EqualTo(20));
    }

    [Test]
    public void AudioAlone_OnASuspendedVideoLink_GetsLongPacketsAtOnce()
    {
        var audio = new AudioPacketization { MaxFrameMs = 60 };
        Assert.That(audio.Update(0, videoSuspended: true, 0), Is.EqualTo(60),
            "video stood down for bandwidth: every byte of framing is taken from the voice");
        Assert.That(audio.Update(0, videoSuspended: false, 1_000), Is.Null);
        Assert.That(audio.Update(0, videoSuspended: false, 6_100), Is.EqualTo(20), "video back with room for it: latency first again");
    }

    [Test]
    public void APeerThatOnlyPlays20msPackets_NeverGetsLongerOnes()
    {
        var audio = new AudioPacketization { MaxFrameMs = 20 };
        for (var t = 0; t < 10_000; t += 250)
            Assert.That(audio.Update(100, videoSuspended: true, t), Is.Null);
        Assert.That(audio.FrameMs, Is.EqualTo(20));

        // A legacy member joins mid-call: the next epoch lowers the ceiling, and packets shrink at once.
        var mixed = new AudioPacketization { MaxFrameMs = 60 };
        mixed.Update(100, true, 0);
        Assert.That(mixed.FrameMs, Is.EqualTo(60));
        mixed.MaxFrameMs = 20;
        Assert.That(mixed.Update(100, true, 250), Is.EqualTo(20));
    }

    [Test]
    public void ABrowserThatRefusesLongPackets_IsNotAskedAgainAndAgain()
    {
        var audio = new AudioPacketization { MaxFrameMs = 60 };
        Assert.That(audio.Update(100, true, 0), Is.EqualTo(60));
        audio.Applied(20);
        Assert.That(audio.FrameMs, Is.EqualTo(20));
        Assert.That(audio.MaxFrameMs, Is.EqualTo(20), "what the encoder accepted is the ceiling from now on");
        Assert.That(audio.Update(100, true, 5_000), Is.Null);
    }

    [Test]
    public void TheRateLoop_HandsTheFrameLengthToTheHost_WhenTheRelayReportsAScarceLink()
    {
        var pacer = new MediaSendPacer((_, _, _) => ValueTask.CompletedTask);
        var loop = new SendRateLoop(pacer, new SendRateController(900), new VideoRateLadder());
        loop.Audio.MaxFrameMs = 60;
        int? asked = null;
        for (var t = 0L; t <= 5_000 && asked is null; t += SendRateLoop.IntervalMs)
        {
            // The relay's receiver queue stands at 600 ms and drains at 350 kbps: a 512 kbps-class link.
            var report = new Bolt.Protocol.MediaCongestionData
            {
                StreamId = Guid.NewGuid(), Flags = Bolt.Protocol.MediaCongestionFlags.Limited, Receivers = 1,
                QueueDelayMs = 600, AllowedKbps = 350, LayerLimit = 3,
            };
            loop.Signals.OnCongestionReport(report, video: true, t);
            asked = loop.Tick(t).AudioFrameMs;
        }
        Assert.That(loop.Controller.CongestionKbps, Is.InRange(1, 599));
        Assert.That(asked, Is.EqualTo(60));
    }
}
