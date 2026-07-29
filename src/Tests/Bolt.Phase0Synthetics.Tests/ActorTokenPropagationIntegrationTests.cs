using System.Collections.Concurrent;
using System.Text;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Server;
using Bolt.Server.Durable;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MemoryPack;

namespace Bolt.Phase0Synthetics.Tests;

[CancelAfter(30000)]
[NonParallelizable]
public sealed class ActorTokenPropagationIntegrationTests
{
    private static readonly string ActorToken = CreateActorToken();
    private static int _portCounter = 23200;

    [Test]
    public async Task UserPubSubControlFrames_CarryActorToken()
    {
        var port = Interlocked.Increment(ref _portCounter);
        var authorizer = new RecordingAuthorizer();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Services.AddSingleton<IBoltTopicAuthorizer>(authorizer);
        builder.Services.Configure<DurableQueueOptions>(_ => { });
        builder.Services.AddSingleton<IDurableQueueStore, InMemoryDurableQueueStore>();
        builder.Services.AddBoltServer();
        var app = builder.Build();
        app.UseWebSockets();
        app.MapBolt("/bolt");
        app.MapGet("/health", () => "ok");
        var appTask = app.RunAsync();
        BoltClient? client = null;

        try
        {
            await WaitForHealthAsync(port).WaitAsync(TimeSpan.FromSeconds(10));
            client = new BoltClient(
                new Uri($"ws://localhost:{port}/bolt"),
                "portal-test-client",
                "XFramework.Portal",
                new BoltClientOptions { RpcTimeoutSeconds = 5, TransportAttemptTimeoutMs = 5_000 },
                NullLogger<BoltClient>.Instance);
            await client.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));

            using (var transientCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var transient = client
                    .SubscribeAsync<byte[]>("communications.test.transient", transientCts.Token, ActorToken)
                    .GetAsyncEnumerator(transientCts.Token);
                var pending = transient.MoveNextAsync().AsTask();
                await authorizer.WaitForAsync(BoltTopicOperation.Subscribe, durable: false);
                transientCts.Cancel();
                await IgnoreCancellationAsync(pending);
                await IgnoreCancellationAsync(transient.DisposeAsync().AsTask());
                await authorizer.WaitForAsync(BoltTopicOperation.Unsubscribe, durable: false);
            }

            const string durableTopic = "communications.test.durable";
            const string subscriberId = "synthetic-subscriber";
            using (var durableCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var durable = client
                    .SubscribeDurableAsync<byte[]>(durableTopic, subscriberId, durableCts.Token, ActorToken)
                    .GetAsyncEnumerator(durableCts.Token);
                var pending = durable.MoveNextAsync().AsTask();
                await authorizer.WaitForAsync(BoltTopicOperation.Subscribe, durable: true);

                await client.AckAsync(durableTopic, subscriberId, 1, actorAccessToken: ActorToken);
                await authorizer.WaitForAsync(BoltTopicOperation.Ack, durable: true);

                durableCts.Cancel();
                await IgnoreCancellationAsync(pending);
                await IgnoreCancellationAsync(durable.DisposeAsync().AsTask());
                await authorizer.WaitForAsync(BoltTopicOperation.Unsubscribe, durable: true);
            }

            await client.UnregisterDurableSubscriptionWithActorAsync(
                durableTopic,
                subscriberId,
                ActorToken);
            await authorizer.WaitForAsync(BoltTopicOperation.Unsubscribe, durable: true, minimumMatches: 2);

            authorizer.Contexts.Should().OnlyContain(context => context.ActorAccessToken == ActorToken);
        }
        finally
        {
            if (client is not null)
                await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await app.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await app.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await appTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Test]
    public async Task DurableReplay_AckedWhileSubscriptionActive_DoesNotRedeliver()
    {
        var port = Interlocked.Increment(ref _portCounter);
        var authorizer = new RecordingAuthorizer();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Services.AddSingleton<IBoltTopicAuthorizer>(authorizer);
        builder.Services.Configure<DurableQueueOptions>(_ => { });
        builder.Services.AddSingleton<IDurableQueueStore, InMemoryDurableQueueStore>();
        builder.Services.AddBoltServer();
        var app = builder.Build();
        app.UseWebSockets();
        app.MapBolt("/bolt");
        app.MapGet("/health", () => "ok");
        var appTask = app.RunAsync();
        BoltClient? client = null;

        try
        {
            await WaitForHealthAsync(port).WaitAsync(TimeSpan.FromSeconds(10));
            client = new BoltClient(
                new Uri($"ws://localhost:{port}/bolt"),
                "portal-durable-lifecycle",
                "XFramework.Portal",
                new BoltClientOptions { RpcTimeoutSeconds = 5, TransportAttemptTimeoutMs = 5_000 },
                NullLogger<BoltClient>.Instance);
            await client.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));

            const string topic = "communications.test.durable-lifecycle";
            const string subscriberId = "synthetic-lifecycle-subscriber";
            var topicHash = BoltCodec.Fnv1aHash(topic);
            var store = app.Services.GetRequiredService<IDurableQueueStore>();

            using (var registrationCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var registration = client
                    .SubscribeDurableAsync<byte[]>(topic, subscriberId, registrationCts.Token, ActorToken)
                    .GetAsyncEnumerator(registrationCts.Token);
                var pending = registration.MoveNextAsync().AsTask();
                await authorizer.WaitForAsync(BoltTopicOperation.Subscribe, durable: true);
                registrationCts.Cancel();
                await IgnoreCancellationAsync(pending);
                await IgnoreCancellationAsync(registration.DisposeAsync().AsTask());
                await authorizer.WaitForAsync(BoltTopicOperation.Unsubscribe, durable: true);
            }

            await store.AppendAsync(topicHash, subscriberId, MemoryPackSerializer.Serialize(new byte[] { 1 }));

            long acknowledgedSequence;
            using (var replayCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var replay = client
                    .SubscribeDurableAsync<byte[]>(topic, subscriberId, replayCts.Token, ActorToken)
                    .GetAsyncEnumerator(replayCts.Token);
                (await replay.MoveNextAsync()).Should().BeTrue();
                replay.Current.IsReplay.Should().BeTrue();
                acknowledgedSequence = replay.Current.Sequence;
                await replay.Current.AckAsync(replayCts.Token);
                replayCts.Cancel();
                await IgnoreCancellationAsync(replay.DisposeAsync().AsTask());
                await authorizer.WaitForAsync(
                    BoltTopicOperation.Unsubscribe,
                    durable: true,
                    minimumMatches: 2);
            }

            (await store.GetLastAckedSequenceAsync(topicHash, subscriberId))
                .Should().Be(acknowledgedSequence);

            using (var verificationCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var verification = client
                    .SubscribeDurableAsync<byte[]>(topic, subscriberId, verificationCts.Token, ActorToken)
                    .GetAsyncEnumerator(verificationCts.Token);
                var moveNext = verification.MoveNextAsync().AsTask();
                await authorizer.WaitForAsync(
                    BoltTopicOperation.Subscribe,
                    durable: true,
                    minimumMatches: 3);
                var completed = await Task.WhenAny(moveNext, Task.Delay(250));
                completed.Should().NotBe(moveNext, "the acknowledged durable message must not be replayed");
                verificationCts.Cancel();
                await IgnoreCancellationAsync(moveNext);
                await IgnoreCancellationAsync(verification.DisposeAsync().AsTask());
            }
        }
        finally
        {
            if (client is not null)
                await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await app.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await app.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await appTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static string CreateActorToken()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"exp\":{expiresAt}}}"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"e30.{payload}.c2ln";
    }

    private static async Task WaitForHealthAsync(int port)
    {
        using var client = new HttpClient();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                if ((await client.GetAsync($"http://localhost:{port}/health")).IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Bolt test host did not become healthy within 10 seconds.");
    }

    private sealed class RecordingAuthorizer : IBoltTopicAuthorizer
    {
        private readonly ConcurrentQueue<BoltTopicAuthorizationContext> _contexts = new();

        public IReadOnlyCollection<BoltTopicAuthorizationContext> Contexts => _contexts.ToArray();

        public ValueTask<bool> AuthorizeAsync(
            BoltTopicAuthorizationContext context,
            CancellationToken ct = default)
        {
            _contexts.Enqueue(context);
            return ValueTask.FromResult(context.ActorAccessToken == ActorToken);
        }

        public async Task WaitForAsync(
            BoltTopicOperation operation,
            bool durable,
            int minimumMatches = 1)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (_contexts.Count(context =>
                        context.Operation == operation && context.Durable == durable) >= minimumMatches)
                {
                    return;
                }

                await Task.Delay(10);
            }

            Assert.Fail($"Did not observe {minimumMatches} {operation} frame(s) with durable={durable}.");
        }
    }
}
