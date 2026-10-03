using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using Bolt.Protocol.Transport;
using Bolt.Rtc;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// How the relay reads the sidecar's state reports. A data channel can stay "open" while the path under it is
/// gone (ICE disconnected: no packet for seconds) or being rebuilt (an ICE restart). Media handed to it then is
/// lost, so the peer must not count as open: the relay sends that participant's media on its WebSocket until
/// ICE is connected again, and only a failed or closed peer ends the session.
/// </summary>
public sealed class SidecarRtcPeerStateTests
{
    private sealed class Sidecar : IAsyncDisposable
    {
        public SidecarRtcPeer Peer { get; }
        public List<RtcChannelState> Changes { get; } = [];

        private Sidecar(SidecarRtcPeer peer) => Peer = peer;

        public static async Task<Sidecar> StartAsync()
        {
            var pipe = new Pipe();
            var stream = new Duplex(pipe.Reader.AsStream());
            var sidecar = new Sidecar(new SidecarRtcPeer(stream, RtcPeerRole.Answer, RtcDefaults.MaxMessageBytes, NullLogger.Instance));
            sidecar.Writer = pipe.Writer;
            sidecar.Peer.StateChanged += state => { lock (sidecar.Changes) sidecar.Changes.Add(state); };
            await sidecar.Peer.StartAsync([], CancellationToken.None);
            return sidecar;
        }

        private PipeWriter Writer { get; set; } = null!;

        public async Task ReportAsync(string ice, string peer, string channel)
        {
            var json = Encoding.UTF8.GetBytes($$"""{"ice":"{{ice}}","peer":"{{peer}}","channel":"{{channel}}"}""");
            var frame = new byte[5 + json.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)(1 + json.Length));
            frame[4] = 0x04;
            json.CopyTo(frame, 5);
            await Writer.WriteAsync(frame);
            await Writer.FlushAsync();
        }

        public async Task<RtcChannelState> SettledAsync(int changes)
        {
            var deadline = Environment.TickCount64 + 3000;
            while (true)
            {
                lock (Changes) if (Changes.Count >= changes) return Peer.State;
                if (Environment.TickCount64 > deadline) return Peer.State;
                await Task.Delay(10);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Writer.CompleteAsync();
            await Peer.DisposeAsync();
        }
    }

    /// <summary>Reads what the "sidecar" writes; swallows what the peer writes (hello, close).</summary>
    private sealed class Duplex(Stream input) : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => input.ReadAsync(buffer, ct);
        public override void Write(byte[] buffer, int offset, int count) { }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => ValueTask.CompletedTask;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    [Test]
    public async Task IceDisconnected_UnderAnOpenChannel_IsNotOpen_AndConnectedAgainIs()
    {
        await using var sidecar = await Sidecar.StartAsync();
        await sidecar.ReportAsync("connected", "connected", "open");
        Assert.That(await sidecar.SettledAsync(1), Is.EqualTo(RtcChannelState.Open));

        await sidecar.ReportAsync("disconnected", "disconnected", "open");
        var stalled = await sidecar.SettledAsync(2);
        Assert.Multiple(() =>
        {
            Assert.That(stalled, Is.Not.EqualTo(RtcChannelState.Open), "no packet has arrived for seconds: media must not go there");
            Assert.That(stalled, Is.Not.EqualTo(RtcChannelState.Closed).And.Not.EqualTo(RtcChannelState.Failed), "it may still come back");
            Assert.That(sidecar.Peer.TrySend([0x21]), Is.False);
        });

        await sidecar.ReportAsync("connected", "connected", "open");
        Assert.That(await sidecar.SettledAsync(3), Is.EqualTo(RtcChannelState.Open));
        Assert.That(sidecar.Peer.TrySend([0x21]), Is.True);
    }

    [Test]
    public async Task AnIceRestart_IsNotOpenWhileIceChecks()
    {
        await using var sidecar = await Sidecar.StartAsync();
        await sidecar.ReportAsync("connected", "connected", "open");
        await sidecar.SettledAsync(1);
        // What pion reports on the answering side of a restart (see sidecar/restart_test.go).
        await sidecar.ReportAsync("checking", "connected", "open");
        Assert.That(await sidecar.SettledAsync(2), Is.Not.EqualTo(RtcChannelState.Open));
        await sidecar.ReportAsync("connected", "connecting", "open");
        Assert.That(await sidecar.SettledAsync(3), Is.EqualTo(RtcChannelState.Open), "ICE is back; pion may still call the peer connecting");
    }

    [Test]
    public async Task IceFailed_EndsThePeer()
    {
        await using var sidecar = await Sidecar.StartAsync();
        await sidecar.ReportAsync("connected", "connected", "open");
        await sidecar.SettledAsync(1);
        await sidecar.ReportAsync("disconnected", "disconnected", "open");
        await sidecar.SettledAsync(2);
        await sidecar.ReportAsync("failed", "failed", "open");
        await sidecar.SettledAsync(3);
        Assert.That(sidecar.Peer.State, Is.EqualTo(RtcChannelState.Failed));
    }
}
