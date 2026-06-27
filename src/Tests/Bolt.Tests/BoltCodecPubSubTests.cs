using System.Buffers;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using FluentAssertions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class BoltCodecPubSubTests
{
    [Test]
    public void Subscribe_RoundTrip_DurableTrue()
    {
        var writer = new RentedBufferWriter(256);
        BoltCodec.WriteSubscribe(writer, "chat.room.42", "user-abc", durable: true);

        var ok = BoltCodec.TryReadSubscribe(writer.WrittenSpan, out var topicHash, out var durable, out var subscriberId, out var topic, out var consumed);

        ok.Should().BeTrue();
        topicHash.Should().Be(BoltCodec.Fnv1aHash("chat.room.42"));
        durable.Should().BeTrue();
        subscriberId.Should().Be("user-abc");
        topic.Should().Be("chat.room.42");
        consumed.Should().Be(writer.WrittenCount);
    }

    [Test]
    public void Subscribe_RoundTrip_DurableFalse()
    {
        var writer = new RentedBufferWriter(256);
        BoltCodec.WriteSubscribe(writer, "presence", "client-1", durable: false);

        BoltCodec.TryReadSubscribe(writer.WrittenSpan, out _, out var durable, out _, out _, out _).Should().BeTrue();
        durable.Should().BeFalse();
    }

    [Test]
    public void Subscribe_RoundTrip_WithActorAccessToken()
    {
        var writer = new RentedBufferWriter(256);
        BoltCodec.WriteSubscribe(writer, "communications.tenant.user", "user-sub", durable: true, actorAccessToken: "actor-token");

        var ok = BoltCodec.TryReadSubscribe(
            writer.WrittenSpan,
            out _,
            out var durable,
            out var subscriberId,
            out var topic,
            out var actorAccessToken,
            out var consumed);

        ok.Should().BeTrue();
        durable.Should().BeTrue();
        subscriberId.Should().Be("user-sub");
        topic.Should().Be("communications.tenant.user");
        actorAccessToken.Should().Be("actor-token");
        consumed.Should().Be(writer.WrittenCount);
    }

    [Test]
    public void Subscribe_WithForgedTopicHash_IsRejected()
    {
        var writer = new RentedBufferWriter(256);
        BoltCodec.WriteSubscribe(writer, "communications.tenant.allowed", "user-sub", durable: true);
        var bytes = writer.WrittenSpan.ToArray();
        BitConverter.GetBytes(BoltCodec.Fnv1aHash("communications.tenant.other")).CopyTo(bytes, 1);

        var ok = BoltCodec.TryReadSubscribe(
            bytes,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        ok.Should().BeFalse();
    }

    [Test]
    public void Unsubscribe_RoundTrip()
    {
        var writer = new RentedBufferWriter(256);
        BoltCodec.WriteUnsubscribe(writer, "chat.room.42", "user-abc");

        var ok = BoltCodec.TryReadUnsubscribe(writer.WrittenSpan, out var topicHash, out var topic, out var subscriberId, out var permanent, out var consumed);

        ok.Should().BeTrue();
        topicHash.Should().Be(BoltCodec.Fnv1aHash("chat.room.42"));
        topic.Should().Be("chat.room.42");
        subscriberId.Should().Be("user-abc");
        permanent.Should().BeTrue();
        consumed.Should().Be(writer.WrittenCount);
    }

    [Test]
    public void Unsubscribe_RoundTrip_DetachOnly()
    {
        var writer = new RentedBufferWriter(256);
        BoltCodec.WriteUnsubscribe(writer, "chat.room.42", "user-abc", permanent: false);

        var ok = BoltCodec.TryReadUnsubscribe(writer.WrittenSpan, out _, out _, out _, out var permanent, out var consumed);

        ok.Should().BeTrue();
        permanent.Should().BeFalse();
        consumed.Should().Be(writer.WrittenCount);
    }

    [Test]
    public void Unsubscribe_RoundTrip_WithActorAccessToken()
    {
        var writer = new RentedBufferWriter(256);
        BoltCodec.WriteUnsubscribe(writer, "chat.room.42", "user-abc", permanent: false, actorAccessToken: "actor-token");

        var ok = BoltCodec.TryReadUnsubscribe(
            writer.WrittenSpan,
            out _,
            out var topic,
            out var subscriberId,
            out var permanent,
            out var actorAccessToken,
            out var consumed);

        ok.Should().BeTrue();
        topic.Should().Be("chat.room.42");
        subscriberId.Should().Be("user-abc");
        permanent.Should().BeFalse();
        actorAccessToken.Should().Be("actor-token");
        consumed.Should().Be(writer.WrittenCount);
    }

    [Test]
    public void Publish_RoundTrip_DurableEligible()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var writer = new RentedBufferWriter(256);
        BoltCodec.WritePublish(writer, "chat.room.42", durableEligible: true, payload);

        var ok = BoltCodec.TryReadPublish(writer.WrittenSpan, out var topicHash, out var topic, out var durableEligible, out var payloadOffset, out var payloadLength, out var totalSize);

        ok.Should().BeTrue();
        topicHash.Should().Be(BoltCodec.Fnv1aHash("chat.room.42"));
        topic.Should().Be("chat.room.42");
        durableEligible.Should().BeTrue();
        payloadLength.Should().Be(payload.Length);
        totalSize.Should().Be(writer.WrittenCount);
        writer.WrittenSpan.Slice(payloadOffset, payloadLength).ToArray().Should().Equal(payload);
    }

    [Test]
    public void Event_RoundTrip_WithSequenceAndReplay()
    {
        var payload = new byte[] { 9, 8, 7 };
        var writer = new RentedBufferWriter(256);
        BoltCodec.WriteEvent(writer, BoltCodec.Fnv1aHash("topic-x"), sequenceNumber: 42, isReplay: true, payload);

        var ok = BoltCodec.TryReadEvent(writer.WrittenSpan, out var topicHash, out var seq, out var isReplay, out var off, out var len, out var total);

        ok.Should().BeTrue();
        topicHash.Should().Be(BoltCodec.Fnv1aHash("topic-x"));
        seq.Should().Be(42);
        isReplay.Should().BeTrue();
        len.Should().Be(payload.Length);
        writer.WrittenSpan.Slice(off, len).ToArray().Should().Equal(payload);
    }

    [Test]
    public void Event_RoundTrip_WithDurableSubscriberId()
    {
        var payload = new byte[] { 9, 8, 7 };
        var writer = new RentedBufferWriter(256);
        BoltCodec.WriteEvent(
            writer,
            BoltCodec.Fnv1aHash("topic-x"),
            subscriberId: "subscriber-7",
            sequenceNumber: 42,
            isReplay: true,
            payload);

        var ok = BoltCodec.TryReadEvent(
            writer.WrittenSpan,
            out var topicHash,
            out var seq,
            out var isReplay,
            out var subscriberId,
            out var off,
            out var len,
            out var total);

        ok.Should().BeTrue();
        topicHash.Should().Be(BoltCodec.Fnv1aHash("topic-x"));
        seq.Should().Be(42);
        isReplay.Should().BeTrue();
        subscriberId.Should().Be("subscriber-7");
        len.Should().Be(payload.Length);
        total.Should().Be(writer.WrittenCount);
        writer.WrittenSpan.Slice(off, len).ToArray().Should().Equal(payload);
    }

    [Test]
    public void Ack_RoundTrip()
    {
        var writer = new RentedBufferWriter(256);
        BoltCodec.WriteAck(writer, "topic-x", "subscriber-7", upToSequenceNumber: 100);

        var ok = BoltCodec.TryReadAck(writer.WrittenSpan, out var topicHash, out var topic, out var sid, out var upTo, out var consumed);

        ok.Should().BeTrue();
        topicHash.Should().Be(BoltCodec.Fnv1aHash("topic-x"));
        topic.Should().Be("topic-x");
        sid.Should().Be("subscriber-7");
        upTo.Should().Be(100);
        consumed.Should().Be(writer.WrittenCount);
    }

    [Test]
    public void Ack_RoundTrip_WithActorAccessToken()
    {
        var writer = new RentedBufferWriter(256);
        BoltCodec.WriteAck(writer, "topic-x", "subscriber-7", upToSequenceNumber: 100, actorAccessToken: "actor-token");

        var ok = BoltCodec.TryReadAck(
            writer.WrittenSpan,
            out _,
            out var topic,
            out var sid,
            out var upTo,
            out var actorAccessToken,
            out var consumed);

        ok.Should().BeTrue();
        topic.Should().Be("topic-x");
        sid.Should().Be("subscriber-7");
        upTo.Should().Be(100);
        actorAccessToken.Should().Be("actor-token");
        consumed.Should().Be(writer.WrittenCount);
    }
}
