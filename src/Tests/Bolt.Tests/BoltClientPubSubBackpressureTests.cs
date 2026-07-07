using System.Reflection;
using System.Threading.Channels;
using Bolt.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class BoltClientPubSubBackpressureTests
{
    [Test]
    public async Task SubscribeAsync_WhenPubSubBufferIsFull_DropsOldestUnreadEvent()
    {
        await using var client = CreateClient(pubSubCapacity: 2);
        var channel = CreateTransientPubSubChannel<byte[]>(client);

        channel.Writer.TryWrite([1]).Should().BeTrue();
        channel.Writer.TryWrite([2]).Should().BeTrue();
        channel.Writer.TryWrite([3]).Should().BeTrue();

        channel.Reader.TryRead(out var first).Should().BeTrue();
        first.Should().Equal(2);
        channel.Reader.TryRead(out var second).Should().BeTrue();
        second.Should().Equal(3);
        channel.Reader.TryRead(out _).Should().BeFalse();
    }

    [Test]
    public async Task SubscribeAsync_WhenPubSubCapacityIsInvalid_UsesMinimumCapacityOne()
    {
        await using var client = CreateClient(pubSubCapacity: 0);
        var channel = CreateTransientPubSubChannel<byte[]>(client);

        channel.Writer.TryWrite([1]).Should().BeTrue();
        channel.Writer.TryWrite([2]).Should().BeTrue();

        channel.Reader.TryRead(out var payload).Should().BeTrue();
        payload.Should().Equal(2);
        channel.Reader.TryRead(out _).Should().BeFalse();
    }

    private static BoltClient CreateClient(int pubSubCapacity) =>
        new(
            new Uri("ws://localhost/bolt"),
            "pubsub_backpressure_test",
            "PubSubBackpressureTest",
            new BoltClientOptions { PubSubChannelCapacity = pubSubCapacity },
            NullLogger.Instance);

    [Test]
    public async Task SubscribeDurableAsync_WhenPubSubBufferIsFull_DoesNotDropUnreadDurableEvent()
    {
        await using var client = CreateClient(pubSubCapacity: 1);
        var channel = CreateDurablePubSubChannel<(long Sequence, bool IsReplay, byte[] Payload)>(client);

        channel.Writer.TryWrite((1, false, [1])).Should().BeTrue();
        channel.Writer.TryWrite((2, false, [2])).Should().BeFalse();

        channel.Reader.TryRead(out var entry).Should().BeTrue();
        entry.Sequence.Should().Be(1);
        entry.Payload.Should().Equal(1);
        channel.Reader.TryRead(out _).Should().BeFalse();
    }

    private static Channel<T> CreateTransientPubSubChannel<T>(BoltClient client) =>
        CreatePrivateChannel<T>(client, "CreateTransientPubSubChannel");

    private static Channel<T> CreateDurablePubSubChannel<T>(BoltClient client) =>
        CreatePrivateChannel<T>(client, "CreateDurablePubSubChannel");

    private static Channel<T> CreatePrivateChannel<T>(BoltClient client, string methodName)
    {
        var method = typeof(BoltClient).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (Channel<T>)method!.MakeGenericMethod(typeof(T)).Invoke(client, null)!;
    }
}
