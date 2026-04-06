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
}
