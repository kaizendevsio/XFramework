using System.Buffers;
using System.Reflection;
using System.Threading.Channels;
using Bolt.Client;
using Bolt.Media;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltMediaEnablementSafetyTests
{
    [Test]
    public void FecEncoder_DiscontinuousSequence_StartsNewBoundedGroup()
    {
        var encoder = new FecEncoder(2);

        encoder.AddFrame(10, new byte[] { 1 }).Should().BeNull();
        encoder.AddFrame(12, new byte[] { 2 }).Should().BeNull();
        var result = encoder.AddFrame(13, new byte[] { 3 });

        result.Should().NotBeNull();
        result!.GroupStartSequence.Should().Be(12);
        result.GroupSize.Should().Be(2);
    }

    [Test]
    public void FecDecoder_RecoversGroupAcrossSequenceWrap()
    {
        var sequences = new[] { uint.MaxValue - 1, uint.MaxValue, 0u, 1u };
        byte[][] frames = [[1, 2], [3, 4], [5, 6], [7, 8]];
        var encoder = new FecEncoder(4);
        FecResult? parity = null;
        for (var index = 0; index < sequences.Length; index++)
            parity = encoder.AddFrame(sequences[index], frames[index]);

        var decoder = new FecDecoder();
        decoder.AddFrame(sequences[0], frames[0]);
        decoder.AddFrame(sequences[1], frames[1]);
        decoder.AddFrame(sequences[3], frames[3]);
        decoder.AddFecFrame(parity!.GroupStartSequence, parity.GroupSize, parity.ParityData, parity.OriginalLengths);

        decoder.TryRecover(0, parity.GroupStartSequence, out var recovered).Should().BeTrue();
        recovered.Should().Equal(frames[2]);
    }

    [Test]
    public void FecDecoder_ExpiredGroup_CannotRecover()
    {
        var time = new ManualTimeProvider();
        var decoder = new FecDecoder(time, TimeSpan.FromSeconds(1));
        var encoder = new FecEncoder(4);
        byte[][] frames = [[1], [2], [3], [4]];
        FecResult? parity = null;
        for (uint sequence = 0; sequence < frames.Length; sequence++)
            parity = encoder.AddFrame(sequence, frames[sequence]);

        decoder.AddFrame(0, frames[0]);
        decoder.AddFrame(1, frames[1]);
        decoder.AddFrame(3, frames[3]);
        decoder.AddFecFrame(0, 4, parity!.ParityData, parity.OriginalLengths);
        time.Advance(TimeSpan.FromSeconds(2));

        decoder.TryRecover(2, 0, out _).Should().BeFalse();
    }

    [Test]
    public async Task MediaStream_FecParity_RecoversPreviouslyReceivedGroup()
    {
        var connection = new BoltConnection(new NoopConnection());
        await using var stream = new BoltMediaStream(connection, Guid.NewGuid(), Guid.NewGuid(), true);
        stream.EnableFec(4);

        byte[][] original = [[10], [20], [30], [40]];
        var encoder = new FecEncoder(4);
        FecResult? parity = null;
        for (uint sequence = 0; sequence < original.Length; sequence++)
            parity = encoder.AddFrame(sequence, original[sequence]);

        await stream.EnqueueFrameAsync(0, 0, original[0], 0);
        await stream.EnqueueFrameAsync(2, 1_920, original[2], 0);
        await stream.EnqueueFrameAsync(3, 2_880, original[3], 0);

        var payload = new byte[parity!.GroupSize * sizeof(int) + parity.ParityData.Length];
        for (var index = 0; index < parity.OriginalLengths.Length; index++)
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                payload.AsSpan(index * sizeof(int)), parity.OriginalLengths[index]);
        parity.ParityData.CopyTo(payload, parity.GroupSize * sizeof(int));

        await stream.EnqueueFecFrameAsync(parity.GroupStartSequence, parity.GroupSize, payload);

        var inbound = GetInboundChannel(stream);
        var received = new List<MediaFrameData>();
        while (inbound.Reader.TryRead(out var frame))
            received.Add(frame);
        received.Any(frame => frame.SequenceNumber == 1 && frame.Data.ToArray().SequenceEqual(original[1]))
            .Should().BeTrue();

        connection.CompleteSendChannel();
    }

    [Test]
    public async Task MediaStream_RequiredEncryption_IsFailClosed()
    {
        var connection = new BoltConnection(new NoopConnection());
        await using var stream = new BoltMediaStream(connection, Guid.NewGuid(), Guid.NewGuid(), true);
        stream.SetEncryption(new StubEncryption(isReady: false));

        Func<Task> send = async () => await stream.SendFrameAsync(new byte[] { 1, 2, 3 });
        await send.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ready authenticated key*");

        await stream.EnqueueFrameAsync(1, 960, new byte[] { 1, 2, 3 }, flags: 0);
        GetInboundChannel(stream).Reader.TryRead(out _).Should().BeFalse();

        connection.CompleteSendChannel();
    }

    [Test]
    public async Task MediaStream_EncryptedFrameWithoutKey_IsDropped()
    {
        var connection = new BoltConnection(new NoopConnection());
        await using var stream = new BoltMediaStream(connection, Guid.NewGuid(), Guid.NewGuid(), true);

        await stream.EnqueueFrameAsync(1, 960, new byte[] { 1, 2, 3 }, flags: 0x10);

        GetInboundChannel(stream).Reader.TryRead(out _).Should().BeFalse();
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task BoltMediaClient_EncryptedCall_IsRejectedBeforeTransportUse()
    {
        await using var client = new BoltClient(
            new Uri("ws://localhost/bolt"),
            "media-test",
            "Media Test",
            new BoltClientOptions(),
            NullLogger<BoltClient>.Instance);
        await using var media = new BoltMediaClient(client, NullLogger<BoltMediaClient>.Instance);

        Func<Task> start = async () => await media.StartCallAsync("peer", encrypted: true);

        await start.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*authenticated peer identities*");
        BoltMediaClient.BuiltInAuthenticatedEncryptionAvailable.Should().BeFalse();
    }

    [Test]
    public async Task BoltMediaClient_RemoteConfig_RegistersOnceAndEndCleansStream()
    {
        await using var client = CreateClientWithConnection(out var connection);
        await using var media = new BoltMediaClient(client, NullLogger<BoltMediaClient>.Instance);
        var callId = await media.StartCallAsync("peer", encrypted: false);
        var streamId = Guid.NewGuid();
        var configuredCount = 0;
        media.OnMediaStreamConfigured += _ => configuredCount++;

        var writer = new ArrayBufferWriter<byte>();
        BoltCodec.WriteMediaConfig(
            writer, streamId, callId, MediaType.Audio, CodecId.Opus,
            48_000, 1, 64, 0, ReadOnlySpan<byte>.Empty);
        var frame = writer.WrittenMemory.ToArray();
        InvokeMediaConfigHandler(media, connection, frame);
        InvokeMediaConfigHandler(media, connection, frame);

        media.GetMediaStream(streamId).Should().NotBeNull();
        configuredCount.Should().Be(1);

        await media.EndCallAsync(callId);
        media.GetMediaStream(streamId).Should().BeNull();
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task NackTracker_GapDetection_IsWrapSafeAndBounded()
    {
        var connection = new BoltConnection(new NoopConnection());
        await using var tracker = new NackTracker(connection, Guid.NewGuid());

        tracker.RecordReceived(uint.MaxValue - 1);
        tracker.RecordReceived(1);

        var missing = GetMissingSequences(tracker);
        missing.Should().BeEquivalentTo([uint.MaxValue, 0u]);

        tracker.RecordReceived(1_000);
        GetMissingSequences(tracker).Should().BeEmpty();
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task BoltMediaClient_TransportDisconnected_ReleasesStreamsAndNotifiesCallEnded()
    {
        await using var client = CreateClientWithConnection(out var connection);
        await using var media = new BoltMediaClient(client, NullLogger<BoltMediaClient>.Instance);
        var callId = await media.StartCallAsync("peer");
        var stream = new BoltMediaStream(connection, Guid.NewGuid(), callId, true);
        media.RegisterMediaStream(stream).Should().BeTrue();
        var ended = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        media.OnCallEnded += id => { ended.TrySetResult(id); return Task.CompletedTask; };

        var disconnected = (Action?)typeof(BoltClient)
            .GetField("Disconnected", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client);
        disconnected!.Invoke();

        (await ended.Task.WaitAsync(TimeSpan.FromSeconds(2))).Should().Be(callId);
        media.GetMediaStream(stream.StreamId).Should().BeNull();
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task LocalVideo_ReceivesCongestionAndKeyframeFeedback_AfterQualityChange()
    {
        await using var client = CreateClientWithConnection(out var connection);
        await using var media = new BoltMediaClient(client, NullLogger<BoltMediaClient>.Instance);
        var callId = await media.StartCallAsync("peer");
        var stream = new BoltMediaStream(connection, Guid.NewGuid(), callId, false);
        media.RegisterMediaStream(stream).Should().BeTrue();
        var bitrates = new List<int>();
        var keyframes = 0;
        stream.OnBitrateChanged += bitrates.Add;
        stream.OnKeyframeNeeded += () => keyframes++;
        media.ConfigureVideoFeedback(stream.StreamId, 3_800);
        media.ConfigureVideoFeedback(stream.StreamId, 21_000);

        foreach (var hint in new[] { QualityHint.Increase, QualityHint.Decrease, QualityHint.KeyframeNeeded })
        {
            var writer = new ArrayBufferWriter<byte>();
            BoltCodec.WriteMediaFeedback(writer, stream.StreamId, 200, 0, 0, 0, hint);
            var frame = writer.WrittenMemory.ToArray();
            typeof(BoltMediaClient).GetMethod("HandleMediaFeedback", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(media, [connection, frame, frame.Length]);
        }

        bitrates.Should().Equal(23_100, 17_325); // A healthy 4K stream is not clamped to the old 10 Mbps limit.
        keyframes.Should().Be(1);
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task LocalStreams_GetTheRelaysCongestionReports_OthersAreIgnored()
    {
        await using var client = CreateClientWithConnection(out var connection);
        await using var media = new BoltMediaClient(client, NullLogger<BoltMediaClient>.Instance);
        var callId = await media.StartCallAsync("peer");
        var stream = new BoltMediaStream(connection, Guid.NewGuid(), callId, false);
        media.RegisterMediaStream(stream).Should().BeTrue();
        var reports = new List<MediaCongestionData>();
        var feedback = new List<MediaFeedbackData>();
        media.OnCongestionReport += reports.Add;
        media.OnReceiverFeedback += feedback.Add;
        foreach (var target in new[] { stream.StreamId, Guid.NewGuid() })
        {
            var writer = new ArrayBufferWriter<byte>();
            BoltCodec.WriteMediaCongestion(writer, new MediaCongestionData { StreamId = target, QueueDelayMs = 120 });
            var frame = writer.WrittenMemory.ToArray();
            typeof(BoltMediaClient).GetMethod("HandleMediaCongestion", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(media, [connection, frame, frame.Length]);
            writer = new ArrayBufferWriter<byte>();
            BoltCodec.WriteMediaFeedback(writer, target, 1, 0, 0, 0, QualityHint.Maintain, 80, 300);
            frame = writer.WrittenMemory.ToArray();
            typeof(BoltMediaClient).GetMethod("HandleMediaFeedback", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(media, [connection, frame, frame.Length]);
        }
        reports.Select(x => x.StreamId).Should().Equal(stream.StreamId);
        feedback.Should().ContainSingle().Which.QueueDelayMs.Should().Be(80);
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task RemoteKeyframeRequests_AreRateLimitedPerStream_UnlessADecoderHasNothingAtAll()
    {
        var transport = new CountingConnection();
        var client = new BoltClient(new Uri("ws://localhost/bolt"), "media-test", "Media Test", new BoltClientOptions(), NullLogger<BoltClient>.Instance);
        var connection = new BoltConnection(transport);
        connection.StartSendLoop(CancellationToken.None);
        ((List<BoltConnection>)typeof(BoltClient).GetField("_connections", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!).Add(connection);
        await using var media = new BoltMediaClient(client, NullLogger<BoltMediaClient>.Instance);
        var callId = await media.StartCallAsync("peer");
        var stream = new BoltMediaStream(connection, Guid.NewGuid(), callId, false);
        media.RegisterMediaStream(stream).Should().BeTrue();
        for (var i = 0; i < 5; i++) await media.RequestRemoteKeyframeAsync(stream.StreamId);
        await media.RequestRemoteKeyframeAsync(stream.StreamId, force: true);
        await Task.Delay(100);
        transport.Count(FrameType.MediaKeyRequest).Should().Be(2, "one per second per stream, plus the forced one");
        connection.CompleteSendChannel();
        await client.DisposeAsync();
    }

    [Test]
    public async Task ReceiverDelayReport_IsOnlySentForAStreamThatIsFlowing()
    {
        var connection = new BoltConnection(new NoopConnection());
        await using var controller = new AdaptiveBitrateController(connection, Guid.NewGuid(), 400, isAudio: true);
        var now = Environment.TickCount64;
        controller.DelayReport(now).Should().BeNull("nothing arrived yet");
        uint ts = 0;
        for (uint sequence = 1; sequence <= 20; sequence++, ts += 960) controller.RecordFrameReceived(sequence, ts, 100);
        controller.DelayReport(now + 250).Should().NotBeNull();
        controller.DelayReport(now + 500).Should().BeNull("a stream that went quiet has no current delay to report");
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task VideoFeedback_FragmentBursts_DoNotLookLikePictureJitter_ButStillDetectLoss()
    {
        var connection = new BoltConnection(new NoopConnection());
        await using var controller = new AdaptiveBitrateController(connection, Guid.NewGuid(), 3_800, false);
        for (uint sequence = 1; sequence <= 200; sequence++)
            controller.RecordFrameReceived(sequence, 90_000);

        var quality = typeof(AdaptiveBitrateController).GetMethod("DetermineQualityHint", BindingFlags.Instance | BindingFlags.NonPublic)!;
        quality.Invoke(controller, null).Should().Be(QualityHint.Increase);
        controller.RecordFrameReceived(240, 90_000);
        quality.Invoke(controller, null).Should().Be(QualityHint.KeyframeNeeded);
        connection.CompleteSendChannel();
    }

    [Test]
    public async Task Feedback_RecoversAfterTransientLoss_AndDoesNotRepeatStaleAdvice()
    {
        var connection = new BoltConnection(new NoopConnection());
        await using var controller = new AdaptiveBitrateController(connection, Guid.NewGuid(), 3800, false);
        var quality = typeof(AdaptiveBitrateController).GetMethod("DetermineQualityHint", BindingFlags.Instance | BindingFlags.NonPublic)!;
        controller.RecordFrameReceived(1, 0);
        controller.RecordFrameReceived(200, 0);
        quality.Invoke(controller, null).Should().Be(QualityHint.KeyframeNeeded);
        quality.Invoke(controller, null).Should().Be(QualityHint.Maintain);
        for (uint i = 201; i <= 400; i++) controller.RecordFrameReceived(i, 0);
        quality.Invoke(controller, null).Should().Be(QualityHint.Increase);
        quality.Invoke(controller, null).Should().Be(QualityHint.Maintain);
        connection.CompleteSendChannel();
    }

    private static Channel<MediaFrameData> GetInboundChannel(BoltMediaStream stream) =>
        (Channel<MediaFrameData>)typeof(BoltMediaStream)
            .GetField("_inbound", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(stream)!;

    private static HashSet<uint> GetMissingSequences(NackTracker tracker) =>
        (HashSet<uint>)typeof(NackTracker)
            .GetField("_missingSeqs", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(tracker)!;

    private static BoltClient CreateClientWithConnection(out BoltConnection connection)
    {
        var client = new BoltClient(
            new Uri("ws://localhost/bolt"),
            "media-test",
            "Media Test",
            new BoltClientOptions(),
            NullLogger<BoltClient>.Instance);
        connection = new BoltConnection(new NoopConnection());
        var connections = (List<BoltConnection>)typeof(BoltClient)
            .GetField("_connections", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        connections.Add(connection);
        return client;
    }

    private static void InvokeMediaConfigHandler(BoltMediaClient media, BoltConnection connection, byte[] frame) =>
        typeof(BoltMediaClient)
            .GetMethod("HandleMediaConfig", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(media, [connection, frame, frame.Length]);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan elapsed) => _utcNow += elapsed;
    }

    private sealed class StubEncryption(bool isReady) : IMediaEncryption
    {
        public byte[] PublicKey => [];
        public bool IsReady { get; } = isReady;
        public int AuthTagSize => 16;
        public void DeriveKey(ReadOnlySpan<byte> remotePublicKeyDer, Guid callId) { }
        public byte[] Encrypt(ReadOnlySpan<byte> plaintext, uint sequenceNumber, Guid streamId) => plaintext.ToArray();
        public byte[] Decrypt(ReadOnlySpan<byte> ciphertextWithTag, uint sequenceNumber, Guid streamId) => ciphertextWithTag.ToArray();
        public void Dispose() { }
    }

    /// <summary>Records what is sent, one frame per write.</summary>
    private sealed class CountingConnection : IBoltConnection
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<byte[]> _sent = new();
        public int Count(FrameType type) => _sent.Count(x => x.Length > 0 && x[0] == (byte)type);
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) { _sent.Enqueue(data.ToArray()); return ValueTask.CompletedTask; }
        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            new(Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => (0, true), TaskScheduler.Default));
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoopConnection : IBoltConnection
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
