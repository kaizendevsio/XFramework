using Bolt.Server.Durable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class InMemoryDurableQueueStoreTests
{
    private InMemoryDurableQueueStore CreateStore(int maxQueueSize = 10_000) =>
        new(Options.Create(new DurableQueueOptions { MaxQueueSize = maxQueueSize }), NullLogger<InMemoryDurableQueueStore>.Instance);

    [Test]
    public async Task Append_AssignsMonotonicSequenceNumbers()
    {
        var store = CreateStore();
        var s1 = await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        var s2 = await store.AppendAsync(1, "sub-a", new byte[] { 2 });
        var s3 = await store.AppendAsync(1, "sub-a", new byte[] { 3 });

        s1.Should().Be(1);
        s2.Should().Be(2);
        s3.Should().Be(3);
    }

    [Test]
    public async Task Append_DifferentSubscribers_HaveIndependentSequences()
    {
        var store = CreateStore();
        var s1 = await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        var s2 = await store.AppendAsync(1, "sub-b", new byte[] { 2 });

        s1.Should().Be(1);
        s2.Should().Be(1);
    }

    [Test]
    public async Task ReadFrom_ReturnsMessagesAfterFromSequence()
    {
        var store = CreateStore();
        await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        await store.AppendAsync(1, "sub-a", new byte[] { 2 });
        await store.AppendAsync(1, "sub-a", new byte[] { 3 });

        var results = new List<(long, byte[])>();
        await foreach (var msg in store.ReadFromAsync(1, "sub-a", fromSequence: 1, maxCount: 100))
            results.Add(msg);

        results.Should().HaveCount(2);
        results[0].Item1.Should().Be(2);
        results[1].Item1.Should().Be(3);
    }

    [Test]
    public async Task Ack_RemovesAckedMessages()
    {
        var store = CreateStore();
        await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        await store.AppendAsync(1, "sub-a", new byte[] { 2 });
        await store.AppendAsync(1, "sub-a", new byte[] { 3 });

        await store.AckAsync(1, "sub-a", upToSequence: 2);

        var results = new List<(long, byte[])>();
        await foreach (var msg in store.ReadFromAsync(1, "sub-a", fromSequence: 0, maxCount: 100))
            results.Add(msg);

        results.Should().HaveCount(1);
        results[0].Item1.Should().Be(3);
    }

    [Test]
    public async Task Ack_ForgedFutureSequence_DoesNotSuppressReconnectReplay()
    {
        var store = CreateStore();
        await store.RegisterDurableSubscriberAsync(1, "sub-a");
        var first = await store.AppendAsync(1, "sub-a", new byte[] { 1 });

        await store.AckAsync(1, "sub-a", long.MaxValue);

        (await store.GetLastAckedSequenceAsync(1, "sub-a")).Should().Be(0);
        var second = await store.AppendAsync(1, "sub-a", new byte[] { 2 });

        var reconnectFrom = await store.GetLastAckedSequenceAsync(1, "sub-a");
        var replay = await ReadAllAsync(store, 1, "sub-a", reconnectFrom);
        replay.Select(message => message.Sequence).Should().Equal(first, second);
    }

    [Test]
    public async Task Ack_DuplicateAndStaleSequences_AreIdempotent()
    {
        var store = CreateStore();
        var first = await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        var second = await store.AppendAsync(1, "sub-a", new byte[] { 2 });
        var third = await store.AppendAsync(1, "sub-a", new byte[] { 3 });

        await store.AckAsync(1, "sub-a", second);
        await store.AckAsync(1, "sub-a", second);
        await store.AckAsync(1, "sub-a", first);

        (await store.GetLastAckedSequenceAsync(1, "sub-a")).Should().Be(second);
        var remaining = await ReadAllAsync(store, 1, "sub-a", 0);
        remaining.Select(message => message.Sequence).Should().Equal(third);
    }

    [Test]
    public async Task Ack_ConcurrentValidStaleDuplicateAndFutureSequences_OnlyAdvancesToIssuedMaximum()
    {
        var store = CreateStore();
        var sequences = new List<long>();
        foreach (var value in Enumerable.Range(1, 32))
            sequences.Add(await store.AppendAsync(1, "sub-a", new byte[] { (byte)value }));

        await Task.WhenAll(
            sequences.Select(sequence => store.AckAsync(1, "sub-a", sequence))
                .Concat(sequences.Select(sequence => store.AckAsync(1, "sub-a", sequence)))
                .Append(store.AckAsync(1, "sub-a", long.MaxValue)));

        (await store.GetLastAckedSequenceAsync(1, "sub-a")).Should().Be(sequences[^1]);
        (await ReadAllAsync(store, 1, "sub-a", 0)).Should().BeEmpty();
    }

    [Test]
    public async Task MaxQueueSize_DropsOldestMessages()
    {
        var store = CreateStore(maxQueueSize: 3);
        await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        await store.AppendAsync(1, "sub-a", new byte[] { 2 });
        await store.AppendAsync(1, "sub-a", new byte[] { 3 });
        await store.AppendAsync(1, "sub-a", new byte[] { 4 });
        await store.AppendAsync(1, "sub-a", new byte[] { 5 });

        var results = new List<(long, byte[])>();
        await foreach (var msg in store.ReadFromAsync(1, "sub-a", fromSequence: 0, maxCount: 100))
            results.Add(msg);

        results.Should().HaveCount(3);
        results.Select(r => r.Item1).Should().BeEquivalentTo(new long[] { 3, 4, 5 });
    }

    [Test]
    public async Task RegisterDurableSubscriber_IsIdempotent()
    {
        var store = CreateStore();
        await store.RegisterDurableSubscriberAsync(1, "sub-a");
        await store.RegisterDurableSubscriberAsync(1, "sub-a");
        await store.RegisterDurableSubscriberAsync(1, "sub-b");

        var subs = await store.GetDurableSubscribersAsync(1);
        subs.Should().BeEquivalentTo(new[] { "sub-a", "sub-b" });
    }

    [Test]
    public async Task TryRegisterDurableSubscriber_RejectsNewSubscriberAtCardinalityLimit()
    {
        var store = CreateStore();

        (await store.TryRegisterDurableSubscriberAsync(42, "sub-a", 1)).Should().BeTrue();
        (await store.TryRegisterDurableSubscriberAsync(42, "sub-a", 1)).Should().BeTrue();
        (await store.TryRegisterDurableSubscriberAsync(42, "sub-b", 1)).Should().BeFalse();

        (await store.GetDurableSubscribersAsync(42)).Should().Equal("sub-a");
    }

    [Test]
    public async Task GetLastAckedSequence_ReturnsZeroForUnknownSubscriber()
    {
        var store = CreateStore();
        var seq = await store.GetLastAckedSequenceAsync(1, "unknown");
        seq.Should().Be(0);
    }

    [Test]
    public async Task GetLastAckedSequence_ReturnsLastAcked()
    {
        var store = CreateStore();
        await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        await store.AppendAsync(1, "sub-a", new byte[] { 2 });
        await store.AckAsync(1, "sub-a", upToSequence: 2);

        var seq = await store.GetLastAckedSequenceAsync(1, "sub-a");
        seq.Should().Be(2);
    }

    private static async Task<List<(long Sequence, byte[] Payload)>> ReadAllAsync(
        IDurableQueueStore store,
        int topicHash,
        string subscriberId,
        long fromSequence)
    {
        var messages = new List<(long Sequence, byte[] Payload)>();
        await foreach (var message in store.ReadFromAsync(topicHash, subscriberId, fromSequence, 100))
            messages.Add(message);
        return messages;
    }
}
