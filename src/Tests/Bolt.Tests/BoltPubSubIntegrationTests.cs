using Bolt.Client;
using Bolt.Server;
using Bolt.Server.Durable;
using FluentAssertions;
using MemoryPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Bolt.Tests;

[MemoryPackable]
public partial record TestPubSubMessage(int Id, string Text);

[TestFixture]
public class BoltPubSubIntegrationTests
{
    private WebApplication _app = null!;
    private Uri _serverUri = null!;
    private const int Port = 5891;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{Port}");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Register durable queue store (in-memory) and BoltServer
        builder.Services.Configure<DurableQueueOptions>(opts =>
        {
            opts.MaxQueueSize = 100;
            opts.MaxReplayBatchSize = 100;
        });
        builder.Services.AddSingleton<IDurableQueueStore, InMemoryDurableQueueStore>();
        builder.Services.AddBoltServer();

        _app = builder.Build();
        _app.UseWebSockets();
        _app.MapBolt("/bolt/ws");

        _ = _app.RunAsync();
        await Task.Delay(500);  // Give server time to bind
        _serverUri = new Uri($"ws://localhost:{Port}/bolt/ws");
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private BoltClient CreateClient(string clientId, string clientName)
    {
        return new BoltClient(
            _serverUri,
            clientId,
            clientName,
            new BoltClientOptions { RpcTimeoutSeconds = 5 },
            NullLogger<BoltClient>.Instance);
    }

    [Test]
    public async Task TransientPubSub_BasicFlow_SubscriberReceivesPublishedMessage()
    {
        var publisher = CreateClient("pub-1", "Publisher");
        var subscriber = CreateClient("sub-1", "Subscriber");

        await publisher.ConnectAsync();
        await subscriber.ConnectAsync();

        var received = new List<TestPubSubMessage>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var subTask = Task.Run(async () =>
        {
            await foreach (var msg in subscriber.SubscribeAsync<TestPubSubMessage>("test.topic.basic", cts.Token))
            {
                received.Add(msg);
                if (received.Count >= 1) break;
            }
        });

        await Task.Delay(300);  // Let subscription settle
        await publisher.PublishAsync("test.topic.basic", new TestPubSubMessage(1, "hello"));

        await subTask;
        received.Should().HaveCount(1);
        received[0].Id.Should().Be(1);
        received[0].Text.Should().Be("hello");

        await publisher.DisposeAsync();
        await subscriber.DisposeAsync();
    }

    [Test]
    public async Task TransientPubSub_PublisherDoesNotReceiveOwnMessages()
    {
        var client = CreateClient("self-pub", "SelfPublisher");
        await client.ConnectAsync();

        var received = new List<TestPubSubMessage>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var subTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in client.SubscribeAsync<TestPubSubMessage>("test.topic.echo", cts.Token))
                    received.Add(msg);
            }
            catch (OperationCanceledException) { }
        });

        await Task.Delay(300);
        await client.PublishAsync("test.topic.echo", new TestPubSubMessage(99, "should-not-receive"));

        cts.CancelAfter(TimeSpan.FromMilliseconds(500));
        try { await subTask; } catch { }

        received.Should().BeEmpty("publisher should not receive its own messages");

        await client.DisposeAsync();
    }

    [Test]
    public async Task DurablePubSub_OfflineMessagesQueued_AndReplayedOnReconnect()
    {
        // Phase 1: subscriber registers durable, then disconnects
        var subscriber1 = CreateClient("sub-durable", "DurableSub");
        await subscriber1.ConnectAsync();

        var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var subTask1 = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in subscriber1.SubscribeDurableAsync<TestPubSubMessage>("test.topic.durable", "subscriber-id-x", cts1.Token))
                {
                    // Just register; do not consume or ack yet
                }
            }
            catch (OperationCanceledException) { }
        });
        await Task.Delay(300);  // Let subscription register on Hub

        cts1.Cancel();
        try { await subTask1; } catch { }
        await subscriber1.DisposeAsync();

        // Phase 2: publisher publishes durable messages while subscriber is offline
        var publisher = CreateClient("pub-durable", "DurablePub");
        await publisher.ConnectAsync();
        await publisher.PublishAsync("test.topic.durable", new TestPubSubMessage(1, "msg-1"), durable: true);
        await publisher.PublishAsync("test.topic.durable", new TestPubSubMessage(2, "msg-2"), durable: true);
        await publisher.PublishAsync("test.topic.durable", new TestPubSubMessage(3, "msg-3"), durable: true);
        await Task.Delay(300);
        await publisher.DisposeAsync();

        // Phase 3: subscriber reconnects with same subscriberId, expects replay
        var subscriber2 = CreateClient("sub-durable-2", "DurableSubReconnect");
        await subscriber2.ConnectAsync();

        var received = new List<DurableMessage<TestPubSubMessage>>();
        var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var subTask2 = Task.Run(async () =>
        {
            await foreach (var msg in subscriber2.SubscribeDurableAsync<TestPubSubMessage>("test.topic.durable", "subscriber-id-x", cts2.Token))
            {
                received.Add(msg);
                await msg.AckAsync(cts2.Token);
                if (received.Count >= 3) break;
            }
        });

        await subTask2;

        received.Should().HaveCount(3);
        received[0].Payload.Id.Should().Be(1);
        received[1].Payload.Id.Should().Be(2);
        received[2].Payload.Id.Should().Be(3);
        received.Should().AllSatisfy(m => m.IsReplay.Should().BeTrue());

        await subscriber2.DisposeAsync();
    }

    [Test]
    public async Task DurablePubSub_AckTrimsQueue_NoReplayAfterAck()
    {
        // Subscriber registers, then disconnects
        var sub1 = CreateClient("sub-ack-1", "AckSub");
        await sub1.ConnectAsync();

        var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var subTask1 = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in sub1.SubscribeDurableAsync<TestPubSubMessage>("test.topic.ack", "ack-sub-id", cts1.Token))
                { }
            }
            catch (OperationCanceledException) { }
        });
        await Task.Delay(300);
        cts1.Cancel();
        try { await subTask1; } catch { }
        await sub1.DisposeAsync();

        // Publish 2 durable messages
        var pub = CreateClient("pub-ack", "AckPub");
        await pub.ConnectAsync();
        await pub.PublishAsync("test.topic.ack", new TestPubSubMessage(1, "a"), durable: true);
        await pub.PublishAsync("test.topic.ack", new TestPubSubMessage(2, "b"), durable: true);
        await Task.Delay(300);
        await pub.DisposeAsync();

        // Reconnect subscriber, consume + ack all
        var sub2 = CreateClient("sub-ack-2", "AckSub2");
        await sub2.ConnectAsync();
        var firstRound = new List<DurableMessage<TestPubSubMessage>>();
        var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var subTask2 = Task.Run(async () =>
        {
            await foreach (var msg in sub2.SubscribeDurableAsync<TestPubSubMessage>("test.topic.ack", "ack-sub-id", cts2.Token))
            {
                firstRound.Add(msg);
                await msg.AckAsync(cts2.Token);
                if (firstRound.Count >= 2) break;
            }
        });
        await subTask2;
        await Task.Delay(300);  // Let ack settle
        await sub2.DisposeAsync();
        firstRound.Should().HaveCount(2);

        // Reconnect again — should receive nothing (queue was acked)
        var sub3 = CreateClient("sub-ack-3", "AckSub3");
        await sub3.ConnectAsync();
        var secondRound = new List<DurableMessage<TestPubSubMessage>>();
        var cts3 = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var subTask3 = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in sub3.SubscribeDurableAsync<TestPubSubMessage>("test.topic.ack", "ack-sub-id", cts3.Token))
                    secondRound.Add(msg);
            }
            catch (OperationCanceledException) { }
        });
        try { await subTask3; } catch { }

        secondRound.Should().BeEmpty("acked messages should not be replayed");
        await sub3.DisposeAsync();
    }

    [Test]
    public async Task NonDurablePublish_NotQueuedForDurableSubscribers()
    {
        // Register durable subscriber, disconnect
        var sub1 = CreateClient("sub-nondurable", "NonDurableSub");
        await sub1.ConnectAsync();
        var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var subTask1 = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in sub1.SubscribeDurableAsync<TestPubSubMessage>("test.topic.nondurable", "nondurable-sub-id", cts1.Token)) { }
            }
            catch (OperationCanceledException) { }
        });
        await Task.Delay(300);
        cts1.Cancel();
        try { await subTask1; } catch { }
        await sub1.DisposeAsync();

        // Publish with durable=false
        var pub = CreateClient("pub-nondurable", "NonDurablePub");
        await pub.ConnectAsync();
        await pub.PublishAsync("test.topic.nondurable", new TestPubSubMessage(1, "fan-out-only"), durable: false);
        await Task.Delay(300);
        await pub.DisposeAsync();

        // Reconnect — should receive nothing (was not queued)
        var sub2 = CreateClient("sub-nondurable-2", "NonDurableSub2");
        await sub2.ConnectAsync();
        var received = new List<DurableMessage<TestPubSubMessage>>();
        var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var subTask2 = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in sub2.SubscribeDurableAsync<TestPubSubMessage>("test.topic.nondurable", "nondurable-sub-id", cts2.Token))
                    received.Add(msg);
            }
            catch (OperationCanceledException) { }
        });
        try { await subTask2; } catch { }

        received.Should().BeEmpty("non-durable publishes should not be queued");
        await sub2.DisposeAsync();
    }

    [Test]
    public async Task NonDurablePublish_LiveDurableSubscriberReceivesWithoutAck()
    {
        var subscriber = CreateClient("sub-nondurable-live", "NonDurableLiveSub");
        var publisher = CreateClient("pub-nondurable-live", "NonDurableLivePub");
        await subscriber.ConnectAsync();
        await publisher.ConnectAsync();

        var received = new List<DurableMessage<TestPubSubMessage>>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var subTask = Task.Run(async () =>
        {
            await foreach (var msg in subscriber.SubscribeDurableAsync<TestPubSubMessage>(
                               "test.topic.nondurable.live",
                               "nondurable-live-sub-id",
                               cts.Token))
            {
                received.Add(msg);
                if (received.Count >= 1)
                    break;
            }
        });

        await Task.Delay(300);
        await publisher.PublishAsync(
            "test.topic.nondurable.live",
            new TestPubSubMessage(7, "live-only"),
            durable: false);

        await subTask;

        received.Should().ContainSingle();
        received[0].Payload.Id.Should().Be(7);
        received[0].IsReplay.Should().BeFalse();
        received[0].Sequence.Should().Be(0);
        await received[0].AckAsync(cts.Token);

        await publisher.DisposeAsync();
        await subscriber.DisposeAsync();
    }
}
