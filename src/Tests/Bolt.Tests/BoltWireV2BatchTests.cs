using System.Buffers;
using System.Buffers.Binary;
using Bolt.Protocol;
using FluentAssertions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltWireV2BatchTests
{
    [Test]
    public void RegisterAndAck_ContainExactWireVersion()
    {
        var register = new ArrayBufferWriter<byte>();
        BoltCodec.WriteRegister(register, "client", "Client");

        BoltCodec.TryReadRegister(
                register.WrittenSpan,
                out var version,
                out var clientId,
                out var clientName,
                out var consumed)
            .Should().BeTrue();
        version.Should().Be(BoltCodec.WireVersion);
        clientId.Should().Be("client");
        clientName.Should().Be("Client");
        consumed.Should().Be(register.WrittenCount);

        var ack = new ArrayBufferWriter<byte>();
        BoltCodec.WriteRegisterAck(ack, true);
        BoltCodec.TryReadRegisterAck(ack.WrittenSpan, out var success, out version).Should().BeTrue();
        success.Should().BeTrue();
        version.Should().Be(BoltCodec.WireVersion);
    }

    [Test]
    public void Batch_RoundTripsCompleteFrames()
    {
        var requestCancel = new byte[BoltCodec.RequestCancelSize];
        requestCancel[0] = (byte)FrameType.RequestCancel;
        Guid.NewGuid().TryWriteBytes(requestCancel.AsSpan(1));
        var streamClose = new byte[19];
        streamClose[0] = (byte)FrameType.StreamClose;
        ReadOnlyMemory<byte>[] frames = [requestCancel, streamClose];
        var writer = new ArrayBufferWriter<byte>();

        BoltCodec.WriteBatch(writer, frames);

        BoltCodec.TryReadBatch(writer.WrittenSpan, out var batch).Should().BeTrue();
        batch.Count.Should().Be(2);
        var decoded = new List<byte[]>();
        foreach (var frame in batch)
            decoded.Add(frame.ToArray());
        decoded.Should().HaveCount(2);
        decoded[0].Should().Equal(frames[0].ToArray());
        decoded[1].Should().Equal(frames[1].ToArray());
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(BoltCodec.MaxBatchFrames + 1)]
    public void Batch_InvalidFrameCount_IsRejected(int count)
    {
        var data = new byte[BoltCodec.BatchHeaderSize];
        data[0] = (byte)FrameType.Batch;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), count);

        BoltCodec.TryReadBatch(data, out _).Should().BeFalse();
    }

    [Test]
    public void Batch_MalformedLengthTrailingBytesNestingAndMedia_AreRejected()
    {
        var firstFrame = new byte[BoltCodec.RequestCancelSize];
        var secondFrame = new byte[BoltCodec.RequestCancelSize];
        firstFrame[0] = (byte)FrameType.RequestCancel;
        secondFrame[0] = (byte)FrameType.RequestCancel;
        ReadOnlyMemory<byte>[] validFrames = [firstFrame, secondFrame];
        var writer = new ArrayBufferWriter<byte>();
        BoltCodec.WriteBatch(writer, validFrames);
        var valid = writer.WrittenSpan.ToArray();

        var truncated = valid[..^1];
        BoltCodec.TryReadBatch(truncated, out _).Should().BeFalse();
        BoltCodec.TryReadBatch([.. valid, 0], out _).Should().BeFalse();

        var nested = valid.ToArray();
        nested[BoltCodec.BatchHeaderSize + 4] = (byte)FrameType.Batch;
        BoltCodec.TryReadBatch(nested, out _).Should().BeFalse();

        var media = valid.ToArray();
        media[BoltCodec.BatchHeaderSize + 4] = (byte)FrameType.MediaFrame;
        BoltCodec.TryReadBatch(media, out _).Should().BeFalse();

        var malformedRequest = new byte[BoltCodec.RequestHeaderSize];
        malformedRequest[0] = (byte)FrameType.Request;
        BinaryPrimitives.WriteInt32LittleEndian(malformedRequest.AsSpan(29), 1);
        var malformedFrames = new ReadOnlyMemory<byte>[] { malformedRequest, secondFrame };
        var encode = () => BoltCodec.WriteBatch(new ArrayBufferWriter<byte>(), malformedFrames);
        encode.Should().Throw<ArgumentException>();

        var malformedBatch = WriteUncheckedBatch(malformedFrames);
        BoltCodec.TryReadBatch(malformedBatch, out _).Should().BeFalse();
    }

    [Test]
    public void Batch_OverByteLimit_IsRejectedBeforeEncodingOrDispatch()
    {
        var firstWriter = new ArrayBufferWriter<byte>();
        var secondWriter = new ArrayBufferWriter<byte>();
        BoltCodec.WritePush(
            firstWriter,
            Guid.NewGuid(),
            1,
            2,
            3,
            new byte[BoltCodec.MaxBatchBytes / 2]);
        BoltCodec.WritePush(
            secondWriter,
            Guid.NewGuid(),
            1,
            2,
            3,
            new byte[BoltCodec.MaxBatchBytes / 2]);
        ReadOnlyMemory<byte>[] frames = [firstWriter.WrittenMemory, secondWriter.WrittenMemory];

        var act = () => BoltCodec.WriteBatch(new ArrayBufferWriter<byte>(), frames);
        act.Should().Throw<ArgumentOutOfRangeException>();

        var oversized = new byte[BoltCodec.MaxBatchBytes + 1];
        oversized[0] = (byte)FrameType.Batch;
        BoltCodec.TryReadBatch(oversized, out _).Should().BeFalse();
    }

    private static byte[] WriteUncheckedBatch(IReadOnlyList<ReadOnlyMemory<byte>> frames)
    {
        var length = BoltCodec.BatchHeaderSize + frames.Sum(frame => 4 + frame.Length);
        var batch = new byte[length];
        batch[0] = (byte)FrameType.Batch;
        BinaryPrimitives.WriteInt32LittleEndian(batch.AsSpan(1), frames.Count);
        var offset = BoltCodec.BatchHeaderSize;
        foreach (var frame in frames)
        {
            BinaryPrimitives.WriteInt32LittleEndian(batch.AsSpan(offset), frame.Length);
            offset += 4;
            frame.Span.CopyTo(batch.AsSpan(offset));
            offset += frame.Length;
        }
        return batch;
    }
}
