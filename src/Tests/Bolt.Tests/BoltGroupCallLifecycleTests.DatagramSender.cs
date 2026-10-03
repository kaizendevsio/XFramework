using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using Bolt.Client;
using Bolt.Media;
using Bolt.Media.Congestion;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Rtc;
using Bolt.Server;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// A sender whose media, relay reports and receiver feedback all ride real data channels (the bolt-rtc sidecar on
/// loopback, a browser-like peer without a congestion-window floor on the phone's side), driven by the browser
/// client's real send path: <see cref="MediaTransportClient"/>, <see cref="MediaSendPacer"/> with the channel's
/// buffered amount in its backlog, and <see cref="SendRateLoop"/>. On a fast clean path video must stay on and climb.
/// </summary>
public sealed partial class BoltGroupCallLifecycleTests
{
    private sealed class RealPhone : IAsyncDisposable
    {
        public required BoltClient Client { get; init; }
        public required MediaTransportClient Transport { get; init; }
        public required BridgeTransport Bridge { get; init; }
        public async ValueTask DisposeAsync()
        {
            await Transport.DisposeAsync();
            Bridge.Close();
            await Client.DisposeAsync();
        }
    }

    private static RealPhone ConnectReal(Fixture f, string id, RtcSidecar sidecar)
    {
        var client = new BoltClient(new Uri("wss://localhost/bolt"), id, id, new BoltClientOptions(), NullLogger<BoltClient>.Instance);
        var bridge = new BridgeTransport(f.Peers[id]);
        var connection = new BoltConnection(bridge);
        connection.StartSendLoop(CancellationToken.None);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        ((List<BoltConnection>)typeof(BoltClient).GetField("_connections", flags)!.GetValue(client)!).Add(connection);
        typeof(BoltClient).GetField("_isRegistered", flags)!.SetValue(client, true);
        connection.ReceiveLoop = (Task)typeof(BoltClient).GetMethod("ReceiveLoopAsync", flags)!.Invoke(client, [connection, CancellationToken.None])!;
        // A browser cannot set an SCTP window floor: the phone's peer runs plain SCTP congestion control.
        var transport = new MediaTransportClient(client, (options, ct) => sidecar.CreateAsync(RtcPeerRole.Offer,
            options with { IceServers = [], RelayOnly = false, AllowLoopback = true, MinCwndBytes = 0 }, ct), NullLogger.Instance);
        return new RealPhone { Client = client, Transport = transport, Bridge = bridge };
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Datagram_SenderOnARealChannel_FastPath_KeepsVideoOnAndClimbs()
    {
        var binary = RtcSidecarTests.RequireBinary();
        await using var sidecar = new RtcSidecar(new RtcSidecarOptions { ExecutablePath = binary }, NullLogger<RtcSidecar>.Instance);
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = new BoltMediaTransportOptions
        {
            Peers = new LoopbackFactory(sidecar), IceServers = new FakeIceSource(), RelayOnly = false, RequestSpacingSeconds = 0,
        });
        typeof(BoltServer).GetProperty("AllowLoopback", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Server, true);
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        await using var sender = ConnectReal(f, "b", sidecar);
        await using var receiver = ConnectReal(f, "a", sidecar);
        sender.Transport.Start();
        receiver.Transport.Start();
        await WaitUntil(() => sender.Transport.IsDatagramActive && receiver.Transport.IsDatagramActive, 20_000);

        var audio = await f.Config("b");
        var video = await f.VideoConfig("b");
        var log = new List<string>();
        var result = await RunSenderAsync(sender.Client, sender.Transport, receiver.Client, audio, video, TimeSpan.FromSeconds(20), log);
        foreach (var line in log) TestContext.Out.WriteLine(line);
        Assert.Multiple(() =>
        {
            var story = Environment.NewLine + string.Join(Environment.NewLine, log);
            Assert.That(result.SuspendedTicks, Is.Zero, "a fast clean path never suspends video" + story);
            // The phone side here is pion's SCTP without a window floor, standing in for a browser's: on a busy runner a
            // keyframe burst can stall it for half a second and cost one cut, after which 20 s is too short to climb back.
            // So this asks that the picture climbs from its start (336 kbps) before anything else; how high video goes on
            // a fast path is the harness's job (udp-20mbit-20ms: no suspension, settles at ~17 Mbit/s).
            Assert.That(result.PeakVideoKbps, Is.GreaterThan(600), "and the picture climbs" + story);
            Assert.That(sender.Transport.IsDatagramActive, Is.True);
        });
    }

    [Test]
    [CancelAfter(60_000)]
    public async Task Client_AnUplinkChannelThatStopsDraining_SendsOnTheSocketInstead_AndVideoStaysOn()
    {
        // The channel is open and ICE has not noticed anything, but nothing it is given leaves (no acknowledgment
        // drains it). Counted as the sender's own backlog, it holds the pacer shut: audio ages out and the rate
        // controller, measuring no delivery at all, suspends video on a link whose WebSocket is perfectly fine.
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        await using var a = Connect(f, "a", network);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        var audio = await f.Config("a");
        var video = await f.VideoConfig("a");
        network.Created.Single(x => x.Role == RtcPeerRole.Offer).Stuck = true;

        var log = new List<string>();
        var result = await RunSenderAsync(a.Client, a.Transport, null, audio, video, TimeSpan.FromSeconds(10), log);
        foreach (var line in log) TestContext.Out.WriteLine(line);
        var onSocket = f.Peers["a"].Received.Count(x => x[0] == (byte)FrameType.MediaFrame);
        Assert.Multiple(() =>
        {
            Assert.That(result.SuspendedTicks, Is.Zero, "a channel that stopped draining is not a slow link");
            Assert.That(onSocket, Is.GreaterThan(300), "media went on the WebSocket instead");
            Assert.That(a.Transport.Status.Reason, Is.EqualTo("stalled"));
        });
    }

    internal sealed record SenderResult(int SuspendedTicks, int FinalVideoKbps, int FinalEstimate, int PeakVideoKbps);

    /// <summary>The browser's send path over a synthetic encoder; the receiver answers with the browser's delay reports.</summary>
    private static async Task<SenderResult> RunSenderAsync(BoltClient senderClient, MediaTransportClient datagram, BoltClient? receiverClient,
        Guid audio, Guid video, TimeSpan duration, List<string> log)
    {
        var clock = Stopwatch.StartNew();
        var connection = senderClient.GetPrimaryConnection();
        var pacer = new MediaSendPacer((frame, isAudio, ct) => datagram.TrySend(frame.Span, isAudio) ? ValueTask.CompletedTask : connection.SendAsync(frame, ct),
            () => connection.PendingBytes + datagram.BufferedAmount);
        pacer.Start();
        var ladder = new VideoRateLadder(VideoRateLadder.IndexForHeight(360));
        ladder.Place(400, 0, congested: true);
        var setting = ladder.Current;
        var loop = new SendRateLoop(pacer, new SendRateController(setting.BitrateKbps + 32 + 52), ladder);
        // As BoltMediaService does: a channel that died under the sender restarts the rate control on the socket.
        datagram.PathLost += loop.PathChanged;
        senderClient.RegisterFrameHandler(FrameType.MediaCongestion, (_, buffer, length) =>
        {
            if (BoltCodec.TryReadMediaCongestion(buffer.AsSpan(0, length), out var report))
                loop.Signals.OnCongestionReport(report, report.StreamId == video, Environment.TickCount64);
        });
        senderClient.RegisterFrameHandler(FrameType.MediaFeedback, (_, buffer, length) =>
        {
            if (BoltCodec.TryReadMediaFeedback(buffer.AsSpan(0, length), out var feedback))
                loop.Signals.OnReceiverFeedback(feedback, feedback.StreamId == video, Environment.TickCount64);
        });
        var feedback = new Dictionary<Guid, (MediaQueuingDelayEstimator Delay, long Bytes, double Kbps, uint Highest, long Window)>
        {
            [audio] = (new(), 0, 0, 0, 0), [video] = (new(), 0, 0, 0, 0),
        };
        var sync = new object();
        receiverClient?.RegisterFrameHandler(FrameType.MediaFrame, (_, buffer, length) =>
        {
            if (!BoltCodec.TryReadMediaFrame(buffer.AsSpan(0, length), out var header)) return;
            lock (sync)
            {
                if (!feedback.TryGetValue(header.StreamId, out var state)) return;
                state.Delay.Observe(header.Timestamp, header.StreamId == audio ? 48 : 90, Environment.TickCount64);
                state.Bytes += length;
                state.Highest = Math.Max(state.Highest, header.SequenceNumber);
                feedback[header.StreamId] = state;
            }
        });

        var stop = new CancellationTokenSource(duration);
        var suspended = false;
        var suspendedTicks = 0;
        var peakVideo = 0;
        var audioSequence = 0u;
        var videoSequence = 0u;
        var random = new Random(7);
        var audioLoop = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
            while (await timer.WaitForNextTickAsync(stop.Token).AsTask().ContinueWith(t => !t.IsCanceled && t.Result))
            {
                var sequence = ++audioSequence;
                var frame = Frame(w => BoltCodec.WriteMediaFrame(w, audio, sequence, (uint)(clock.ElapsedMilliseconds * 48), MediaFrameFlags.Encrypted, new byte[80 + 278]));
                pacer.EnqueueAudio(frame);
            }
        });
        var videoLoop = Task.Run(async () =>
        {
            var lastKey = long.MinValue / 2;
            VideoRung? lastRung = null;
            while (!stop.IsCancellationRequested)
            {
                var current = setting;
                try { await Task.Delay(1000 / current.Rung.Framerate, stop.Token); } catch (OperationCanceledException) { break; }
                if (suspended) { lastRung = null; continue; }
                var now = clock.ElapsedMilliseconds;
                var key = lastRung != current.Rung || now - lastKey >= 10_000;
                if (key) lastKey = now;
                lastRung = current.Rung;
                var bytes = (int)(current.BitrateKbps * 1000 / 8.0 / current.Rung.Framerate * (key ? 6 : 1) * (0.85 + random.NextDouble() * 0.3));
                var payload = Math.Max(256, datagram.MaxMessageBytes - BoltCodec.MediaFrameHeaderSize - 278 - 36);
                if (datagram.MaxMessageBytes == 0) payload = 4096;
                var count = Math.Max(1, (bytes + payload - 1) / payload);
                var timestamp = (uint)(now * 90);
                var frames = new List<byte[]>(count);
                for (var i = 0; i < count; i++)
                {
                    var sequence = ++videoSequence;
                    var size = Math.Min(payload, bytes - i * payload) + 278 + 12;
                    frames.Add(Frame(w => BoltCodec.WriteMediaFrame(w, video, sequence, timestamp,
                        (byte)(MediaFrameFlags.Encrypted | (key && i == 0 ? MediaFrameFlags.Keyframe : 0)), new byte[size])));
                }
                pacer.EnqueueVideo(new PacedPicture(frames, key));
            }
        });
        var feedbackLoop = receiverClient is null ? Task.CompletedTask : Task.Run(async () =>
        {
            var receiverConnection = receiverClient.GetPrimaryConnection();
            while (!stop.IsCancellationRequested)
            {
                try { await Task.Delay(250, stop.Token); } catch (OperationCanceledException) { break; }
                var now = Environment.TickCount64;
                var reports = new List<byte[]>();
                lock (sync)
                {
                    foreach (var (stream, state) in feedback.ToArray())
                    {
                        var s = state;
                        var elapsed = s.Window == 0 ? 0 : now - s.Window;
                        s.Window = now;
                        if (s.Bytes == 0) { feedback[stream] = s; continue; }
                        if (elapsed > 0) { var kbps = s.Bytes * 8.0 / elapsed; s.Kbps = s.Kbps <= 0 ? kbps : s.Kbps * 0.7 + kbps * 0.3; }
                        s.Bytes = 0;
                        feedback[stream] = s;
                        reports.Add(Frame(w => BoltCodec.WriteMediaFeedback(w, stream, s.Highest, 0, 0, 0, QualityHint.Maintain,
                            (ushort)Math.Clamp(s.Delay.DelayMs, 0, ushort.MaxValue), (uint)Math.Round(s.Kbps))));
                    }
                }
                foreach (var report in reports) await receiverConnection.SendAsync(report, CancellationToken.None);
            }
        });
        SendRateTick tick = default;
        var lastLog = 0L;
        while (!stop.IsCancellationRequested)
        {
            try { await Task.Delay(SendRateLoop.IntervalMs, stop.Token); } catch (OperationCanceledException) { break; }
            tick = loop.Tick(Environment.TickCount64);
            if (tick.SuspendVideo) suspended = true;
            if (tick.ResumeVideo) suspended = false;
            if (suspended) suspendedTicks++;
            if (tick.Video is { } next) setting = next;
            if (!suspended) peakVideo = Math.Max(peakVideo, setting.BitrateKbps);
            if (clock.ElapsedMilliseconds - lastLog >= 1000)
            {
                lastLog = clock.ElapsedMilliseconds;
                log.Add($"t={clock.Elapsed.TotalSeconds:F0} estimate={tick.Decision.TotalKbps} video={(suspended ? "suspended" : setting.ToString())} delay={tick.Decision.DelayMs} " +
                        $"signal={tick.Decision.Signal} sent={tick.Pacer.SentKbps} local={tick.Pacer.QueueDelayMs} cap={tick.Pacer.CapacityKbps} backlog={tick.Pacer.BacklogBytes} " +
                        $"relay={(tick.Relay is { } r ? $"q{r.QueueDelayMs}/up{r.UplinkDelayMs}/cap{r.CapacityKbps}{(r.Dropping ? "/drop" : "")}" : "-")} " +
                        $"receiver={(tick.Receiver is { } q ? $"q{q.QueueDelayMs}/got{q.ReceivedKbps}" : "-")} path={datagram.Status.Description}");
            }
        }
        await Task.WhenAll(audioLoop, videoLoop, feedbackLoop);
        await pacer.DisposeAsync();
        return new SenderResult(suspendedTicks, suspended ? 0 : setting.BitrateKbps, tick.Decision.TotalKbps, peakVideo);
    }
}
