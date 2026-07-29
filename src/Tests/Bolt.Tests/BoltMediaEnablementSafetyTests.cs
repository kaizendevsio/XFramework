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
