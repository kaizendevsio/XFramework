using System.Buffers;
using Bolt.Protocol;
using FluentAssertions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltCodecWriterValidationTests
{
    [Test]
    public void RequestCancel_RoundTripsRequestId()
    {
        var requestId = Guid.NewGuid();
        var writer = new ArrayBufferWriter<byte>();

        BoltCodec.WriteRequestCancel(writer, requestId);

        BoltCodec.TryReadRequestCancel(writer.WrittenSpan, out var decoded).Should().BeTrue();
        decoded.Should().Be(requestId);
    }

    [Test]
    public void WriteNackRequest_MoreThanUShortEntries_IsRejected()
    {
        var writer = new ArrayBufferWriter<byte>();
        var sequences = new uint[ushort.MaxValue + 1];

        FluentActions.Invoking(() => BoltCodec.WriteNackRequest(writer, Guid.NewGuid(), sequences))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestCase("")]
    [TestCase(null)]
    public void WritePublish_InvalidTopic_IsRejected(string? topic)
    {
        var writer = new ArrayBufferWriter<byte>();

        FluentActions.Invoking(() => BoltCodec.WritePublish(writer, topic!, false, ReadOnlySpan<byte>.Empty))
            .Should().Throw<ArgumentException>();
    }

    [Test]
    public void WriteRequest_FrameAboveProtocolLimit_IsRejected()
    {
        var writer = new ArrayBufferWriter<byte>();
        var payload = new byte[BoltCodec.DefaultMaxFrameBytes];

        FluentActions.Invoking(() => BoltCodec.WriteRequest(writer, Guid.NewGuid(), 1, 2, 3, payload))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}
