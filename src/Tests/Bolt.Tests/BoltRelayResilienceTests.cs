using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// Phase 0 of call network resilience: a slow or briefly dead mobile link must degrade the call,
/// never end it. Covers the relay's per-receiver media queue, the transport progress watchdog,
/// and the client's reconnect discipline.
/// </summary>
[CancelAfter(20_000)]
public sealed class BoltRelayResilienceTests
{
    private static readonly Guid Audio = Guid.NewGuid(), Video = Guid.NewGuid(), OtherVideo = Guid.NewGuid();

    // ── BoltMediaSendQueue ──

    [Test]
    public void Queue_ServesFeedbackThenAudioThenVideo()
    {
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions());
        Assert.That(queue.TryEnqueue(Frame(1), BoltMediaLane.Video, Video, 1, keyStart: true).Queued, Is.True);
        Assert.That(queue.TryEnqueue(Frame(2), BoltMediaLane.Audio, Audio, 1, keyStart: false).Queued, Is.True);
        Assert.That(queue.TryEnqueue(Frame(3), BoltMediaLane.Feedback, Video, 0, keyStart: false).Queued, Is.True);
        Assert.That(Drain(queue), Is.EqualTo(new byte[] { 3, 2, 1 }));
    }

    [Test]
    public void Queue_NewReceiverWaitsForAKeyframe_AndRequestsOneAtMostOncePerInterval()
    {
        var now = 0L;
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions { KeyframeRequestIntervalMs = 1000 }, () => now);
        var first = queue.TryEnqueue(Frame(1), BoltMediaLane.Video, Video, 1, keyStart: false);
        var second = queue.TryEnqueue(Frame(2), BoltMediaLane.Video, Video, 2, keyStart: false);
        now = 1000;
        var third = queue.TryEnqueue(Frame(3), BoltMediaLane.Video, Video, 3, keyStart: false);
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new BoltMediaEnqueueResult(false, true)));
            Assert.That(second, Is.EqualTo(new BoltMediaEnqueueResult(false, false)), "requests are coalesced");
            Assert.That(third, Is.EqualTo(new BoltMediaEnqueueResult(false, true)), "a lost keyframe is asked for again");
        });
        Assert.That(queue.TryEnqueue(Frame(4), BoltMediaLane.Video, Video, 4, keyStart: true).Queued, Is.True);
        Assert.That(queue.TryEnqueue(Frame(5), BoltMediaLane.Video, Video, 5, keyStart: false).Queued, Is.True);
        Assert.That(Drain(queue), Is.EqualTo(new byte[] { 4, 5 }));
    }

    [Test]
    public void Queue_OverBudget_DropsTheStreamsQueuedPictures_KeepsAudio_AndResumesAtAKeyframe()
    {
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions { VideoMaxQueuedBytes = 300, VideoMaxQueueDelayMs = 60_000 });
        Assert.That(queue.TryEnqueue(Frame(1, 100), BoltMediaLane.Video, Video, 1, keyStart: true).Queued, Is.True);
        Assert.That(queue.TryEnqueue(Frame(2, 100), BoltMediaLane.Video, Video, 2, keyStart: false).Queued, Is.True);
        Assert.That(queue.TryEnqueue(Frame(3, 100), BoltMediaLane.Video, OtherVideo, 1, keyStart: true).Queued, Is.True);
        Assert.That(queue.TryEnqueue(Frame(9, 50), BoltMediaLane.Audio, Audio, 1, keyStart: false).Queued, Is.True);

        var overflow = queue.TryEnqueue(Frame(4, 100), BoltMediaLane.Video, Video, 3, keyStart: false);
        Assert.That(overflow, Is.EqualTo(new BoltMediaEnqueueResult(false, true)));
        Assert.That(queue.TryEnqueue(Frame(5, 100), BoltMediaLane.Video, Video, 4, keyStart: false).Queued, Is.False,
            "nothing after a lost picture decodes until the next keyframe");
        Assert.That(queue.TryEnqueue(Frame(6, 100), BoltMediaLane.Video, Video, 5, keyStart: true).Queued, Is.True);
        Assert.That(Drain(queue), Is.EqualTo(new byte[] { 9, 3, 6 }),
            "audio first, the other stream untouched, and the congested stream restarts at its keyframe");
        Assert.That(queue.DroppedVideoFrames, Is.EqualTo(4));
        Assert.That(queue.DroppedAudioFrames, Is.Zero);
    }

    [Test]
    public void Queue_StaleVideo_TriggersCongestion_ButAFreshKeyframeSkipsAhead()
    {
        var now = 0L;
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions { VideoMaxQueueDelayMs = 1000 }, () => now);
        queue.TryEnqueue(Frame(1), BoltMediaLane.Video, Video, 1, keyStart: true);
        queue.TryEnqueue(Frame(2), BoltMediaLane.Video, Video, 2, keyStart: false);
        now = 1500; // the link stalled; what is queued is already late
        Assert.That(queue.TryEnqueue(Frame(3), BoltMediaLane.Video, Video, 3, keyStart: true).Queued, Is.True);
        Assert.That(Drain(queue), Is.EqualTo(new byte[] { 3 }));
    }

    [Test]
    public void Queue_Audio_DropsOldestPastItsDelayBudget()
    {
        var now = 0L;
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions { AudioMaxQueueDelayMs = 500 }, () => now);
        for (byte i = 1; i <= 5; i++) { queue.TryEnqueue(Frame(i), BoltMediaLane.Audio, Audio, i, keyStart: false); now += 200; }
        // Enqueued at 0..800; now is 1000. Everything older than 500 ms is late speech.
        Assert.That(Drain(queue), Is.EqualTo(new byte[] { 4, 5 }));
        Assert.That(queue.DroppedAudioFrames, Is.EqualTo(3));
    }

    [Test]
    public void Queue_DropsRetransmissions_ButTreatsAFarBackwardSequenceAsARestart()
    {
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions());
        queue.TryEnqueue(Frame(1), BoltMediaLane.Audio, Audio, 5000, keyStart: false);
        Assert.That(queue.TryEnqueue(Frame(2), BoltMediaLane.Audio, Audio, 4990, keyStart: false).Queued, Is.False, "a late NACK retransmission");
        Assert.That(queue.TryEnqueue(Frame(3), BoltMediaLane.Audio, Audio, 5000, keyStart: false).Queued, Is.False, "a duplicate");
        Assert.That(queue.TryEnqueue(Frame(4), BoltMediaLane.Audio, Audio, 7, keyStart: false).Queued, Is.True, "a restarted stream");
        Assert.That(queue.StaleFrames, Is.EqualTo(2));
    }

    [Test]
    public void Queue_PersistentCongestion_BacksKeyframeRequestsOff()
    {
        var now = 0L;
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions
        { VideoMaxQueuedBytes = 150, VideoMaxQueueDelayMs = 60_000, KeyframeRequestIntervalMs = 1000 }, () => now);
        uint sequence = 0;
        var requests = new List<long>();
        for (now = 0; now < 20_000; now += 100)
        {
            // A keyframe whose delta cannot fit: every picture overflows the tiny budget.
            var result = queue.TryEnqueue(Frame(1, 100), BoltMediaLane.Video, Video, ++sequence, keyStart: sequence % 10 == 1);
            if (result.RequestKeyframe) requests.Add(now);
            if (queue.TryEnqueue(Frame(1, 100), BoltMediaLane.Video, Video, ++sequence, keyStart: false).RequestKeyframe) requests.Add(now);
        }
        var gaps = requests.Zip(requests.Skip(1), (a, b) => b - a).ToArray();
        Assert.That(gaps, Is.Not.Empty);
        Assert.That(gaps.Max(), Is.GreaterThanOrEqualTo(4000), "a phone that stays congested must not make everyone pay a keyframe a second");
        Assert.That(gaps.Max(), Is.LessThanOrEqualTo(8100));
    }

    // ── BoltHubConnection: progress watchdog and media lanes ──

    [Test]
    public async Task Connection_SlowWriteWithinTheStallWindow_IsNotRetired()
    {
        var transport = new GatedConnection();
        var connection = new BoltHubConnection(transport, 64, sendEnqueueTimeoutMs: 50, 1024 * 1024, transportSendTimeoutMs: 2_000, mediaSendQueue: new());
        Exception? failure = null;
        connection.StartSendLoop(CancellationToken.None, error => failure = error);
        transport.Block();
        connection.TryEnqueueMedia(Frame(1), BoltMediaLane.Audio, Audio, 1);
        await Task.Delay(600); // twelve times the old deadline
        transport.Release();
        await WaitAsync(() => transport.Sent.Count == 1);
        Assert.Multiple(() =>
        {
            Assert.That(failure, Is.Null);
            Assert.That(connection.IsAlive, Is.True);
            Assert.That(connection.TransportSendTimeoutCount, Is.Zero);
        });
        connection.CompleteSendChannel();
        await connection.SendLoop!;
    }

    [Test]
    public async Task Connection_WriteWithoutProgressForTheStallWindow_IsRetired()
    {
        var transport = new GatedConnection();
        var connection = new BoltHubConnection(transport, 64, sendEnqueueTimeoutMs: 50, 1024 * 1024, transportSendTimeoutMs: 400, mediaSendQueue: new());
        var retired = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.StartSendLoop(CancellationToken.None, error => retired.TrySetResult(error));
        transport.Block();
        connection.TryEnqueueMedia(Frame(1), BoltMediaLane.Audio, Audio, 1);
        var error = await retired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(error, Is.TypeOf<BoltTransportSendTimeoutException>());
        Assert.That(connection.TryEnqueueMedia(Frame(2), BoltMediaLane.Audio, Audio, 2).Queued, Is.False);
        Assert.That(() => connection.SendLoop!.GetAwaiter().GetResult(), Throws.InstanceOf<BoltTransportSendTimeoutException>());
    }

    [Test]
    public async Task Connection_MediaNeverBlocksTheCaller_AndControlFramesGoFirst()
    {
        var transport = new GatedConnection();
        var connection = new BoltHubConnection(transport, 64, sendEnqueueTimeoutMs: 50, 1024 * 1024, transportSendTimeoutMs: 10_000, mediaSendQueue: new());
        connection.StartSendLoop(CancellationToken.None);
        transport.Block();
        connection.TryEnqueueMedia(Frame(1), BoltMediaLane.Audio, Audio, 1); // in flight
        await WaitAsync(() => transport.Attempts == 1);
        var started = Environment.TickCount64;
        for (uint i = 2; i < 500; i++) connection.TryEnqueueMedia(Frame(2), BoltMediaLane.Video, Video, i, keyStart: i == 2);
        await connection.SendAsync(Frame(7), CancellationToken.None); // control
        Assert.That(Environment.TickCount64 - started, Is.LessThan(500));
        transport.Release();
        await WaitAsync(() => transport.Sent.Count >= 3);
        Assert.That(transport.Sent[1][0], Is.EqualTo(7), "a control frame overtakes queued media");
        connection.CompleteSendChannel();
        await connection.SendLoop!;
    }

    [Test]
    public async Task Connection_UnderPressure_FiresBelowAByteCapacityEqualToTheOldThreshold()
    {
        var transport = new GatedConnection();
        var connection = new BoltHubConnection(transport, 64, 5_000, sendQueueByteCapacity: 1024 * 1024);
        connection.StartSendLoop(CancellationToken.None);
        transport.Block();
        for (var i = 0; i < 60; i++) await connection.SendAsync(new byte[16 * 1024], CancellationToken.None);
        Assert.That(connection.IsUnderPressure, Is.True, "Yap's 1 MiB cap equalled the 1 MiB threshold, so this never fired");
        transport.Release();
        await WaitAsync(() => !connection.IsUnderPressure);
        connection.CompleteSendChannel();
        await connection.SendLoop!;
    }

    [Test]
    public void SocketTuning_LimitsTheKernelsUnsentBacklogOnLinux_AndIsANoOpElsewhere()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(listener.LocalEndpoint);
        var applied = BoltSocketTuning.TryLimitUnsentBytes(client, 32 * 1024);
        Assert.That(applied, Is.EqualTo(OperatingSystem.IsLinux()));
        if (applied)
        {
            var value = new byte[4];
            client.GetRawSocketOption(6, 25, value);
            Assert.That(BitConverter.ToInt32(value), Is.EqualTo(32 * 1024));
        }
        Assert.That(BoltSocketTuning.TryLimitUnsentBytes(null, 1024), Is.False);
    }

    // ── BoltClient reconnect discipline ──

    [Test]
    public async Task Client_Dispose_StopsTheConnectRetryLoop()
    {
        var client = new BoltClient(new Uri("ws://127.0.0.1:9/bolt"), "retry-client", "retry",
            new BoltClientOptions { TransportAttemptTimeoutMs = 200 }, NullLogger.Instance);
        var retry = client.ConnectWithRetryAsync();
        await Task.Delay(700);
        await client.DisposeAsync();
        // Before the fix the loop ignored disposal and kept retrying for up to ~50 minutes.
        Assert.That(async () => await retry.WaitAsync(TimeSpan.FromSeconds(3)),
            Throws.InstanceOf<OperationCanceledException>().Or.InstanceOf<ObjectDisposedException>());
        Assert.That(() => client.ConnectAsync(), Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public async Task Client_WithoutAutoReconnect_NeverRetriesASpentCredential()
    {
        var client = new BoltClient(new Uri("ws://127.0.0.1:9/bolt"), "ticket-client", "ticket",
            new BoltClientOptions { AutoReconnect = false }, NullLogger.Instance);
        var reconnecting = 0;
        client.Reconnecting += () => reconnecting++;
        var reconnect = (Task)typeof(BoltClient).GetMethod("ReconnectAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(client, null)!;
        await reconnect.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(reconnecting, Is.Zero);
        await client.DisposeAsync();
    }

    // ── helpers ──

    private static byte[] Frame(byte marker, int length = 8)
    {
        var frame = new byte[length];
        frame[0] = marker;
        return frame;
    }

    private static byte[] Drain(BoltMediaSendQueue queue)
    {
        var markers = new List<byte>();
        while (queue.TryDequeue(out var item)) { markers.Add(item.Buffer[0]); BoltMediaSendQueue.Release(item); }
        return markers.ToArray();
    }

    private static byte[] Write(Action<IBufferWriter<byte>> write)
    {
        var writer = new ArrayBufferWriter<byte>();
        write(writer);
        return writer.WrittenSpan.ToArray();
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        var deadline = Environment.TickCount64 + 5_000;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline) Assert.Fail("condition not met within 5 s");
            await Task.Delay(10);
        }
    }

    /// <summary>A transport whose writes can be held, like a socket whose buffer is full.</summary>
    private sealed class GatedConnection : IBoltConnection
    {
        private TaskCompletionSource? _gate;
        private int _attempts;
        public List<byte[]> Sent { get; } = [];
        public int Attempts => Volatile.Read(ref _attempts);
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public void Block() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Release() => _gate?.TrySetResult();

        public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _attempts);
            if (_gate is { } gate) await gate.Task.WaitAsync(ct);
            lock (Sent) Sent.Add(data.ToArray());
        }

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            new(Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => (0, true), TaskScheduler.Default));
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
