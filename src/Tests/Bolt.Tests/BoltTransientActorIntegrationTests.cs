using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltTransientActorIntegrationTests
{
    private WebApplication _app = null!;
    private Uri _uri = null!;
    private ActorTopicAuthorizer _authorizer = null!;

    [SetUp]
    public async Task SetUp()
    {
        _authorizer = new ActorTopicAuthorizer();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddSingleton<IBoltTopicAuthorizer>(_authorizer);
        builder.Services.AddBoltServer(options => options.RequireTopicAuthorization = true);
        _app = builder.Build();
        _app.UseWebSockets();
        _app.MapBolt("/bolt/ws");
        _app.Services.GetRequiredService<BoltServer>().RegisterHandler("barrier", (_, _) =>
            Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty)));
        await _app.StartAsync();
        _uri = new Uri(_app.Urls.Single().Replace("http://", "ws://") + "/bolt/ws");
    }

    [TearDown]
    public async Task TearDown()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(3, true)]
    public async Task SubscribeAsync_SharedConnection_IndependentActorsCancellationAndReconnect(int count, bool explicitUnsubscribe)
    {
        await using var client = CreateClient("shared");
        await using var publisher = CreateClient("publisher");
        await client.ConnectAsync();
        await publisher.ConnectAsync();
        var tokens = Enumerable.Range(0, count).Select(i => Token($"actor-{i}")).ToArray();
        foreach (var token in tokens) _authorizer.Allowed[token] = true;
        var cancellations = Enumerable.Range(0, count).Select(_ => new CancellationTokenSource()).ToArray();
        var subscriptions = Enumerable.Range(0, count).Select(i => client.SubscribeAsync<TestPubSubMessage>(
            "thread.typing", cancellations[i].Token, _ => ValueTask.FromResult<string?>(tokens[i])).GetAsyncEnumerator()).ToArray();
        var pending = subscriptions.Select(x => x.MoveNextAsync().AsTask()).ToArray();
        try
        {
            var initial = await ReadSubscriptionsAsync(count);
            initial.Select(x => x.SubscriberId).Should().OnlyHaveUniqueItems();
            initial.Select(x => x.ActorAccessToken).Should().BeEquivalentTo(tokens);
            await BarrierAsync(client);
            await publisher.PublishAsync("thread.typing", new TestPubSubMessage(1, "first"));
            foreach (var next in pending) (await next.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
            subscriptions.Should().OnlyContain(x => x.Current.Id == 1);

            // Cancel just one actor, leaving every other subscription alive.
            pending = subscriptions.Select(x => x.MoveNextAsync().AsTask()).ToArray();
            await client.UnsubscribeAsync("thread.typing"); // Actorless API cannot remove actor-bound subscriptions.
            if (explicitUnsubscribe)
            {
                await client.UnsubscribeWithActorAsync("thread.typing", tokens[0]);
                (await pending[0].WaitAsync(TimeSpan.FromSeconds(5))).Should().BeFalse();
            }
            else
            {
                cancellations[0].Cancel();
                Func<Task> cancelled = async () => await pending[0];
                await cancelled.Should().ThrowAsync<OperationCanceledException>();
            }
            await BarrierAsync(client);
            await publisher.PublishAsync("thread.typing", new TestPubSubMessage(2, "after cancellation"));
            foreach (var next in pending.Skip(1)) (await next.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
            subscriptions.Skip(1).Should().OnlyContain(x => x.Current.Id == 2);

            // Rebind each remaining actor with its refreshed token, not another user's.
            for (var i = 1; i < count; i++)
            {
                _authorizer.Allowed.TryRemove(tokens[i], out _);
                tokens[i] = Token($"actor-{i}-refreshed");
                _authorizer.Allowed[tokens[i]] = true;
                pending[i] = subscriptions[i].MoveNextAsync().AsTask();
            }
            await client.GetPrimaryConnection().Transport.CloseAsync();
            var rebound = await ReadSubscriptionsAsync(count - 1);
            rebound.Select(x => x.ActorAccessToken).Should().BeEquivalentTo(tokens.Skip(1));
            rebound.Select(x => x.SubscriberId).Should().BeEquivalentTo(initial
                .Where(x => x.ActorAccessToken != tokens[0]).Select(x => x.SubscriberId));
            await BarrierAsync(client);
            await publisher.PublishAsync("thread.typing", new TestPubSubMessage(3, "after reconnect"));
            foreach (var next in pending.Skip(1)) (await next.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
            subscriptions.Skip(1).Should().OnlyContain(x => x.Current.Id == 3);
        }
        finally
        {
            foreach (var cancellation in cancellations) cancellation.Cancel();
            foreach (var next in pending) { try { await next; } catch (OperationCanceledException) { } }
            foreach (var subscription in subscriptions) await subscription.DisposeAsync();
            foreach (var cancellation in cancellations) cancellation.Dispose();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SubscribeAsync_DeniedOrExpiredActor_CannotReceiveAnotherActorsEvents(bool expired)
    {
        await using var client = CreateClient("shared");
        await using var publisher = CreateClient("publisher");
        await client.ConnectAsync();
        await publisher.ConnectAsync();
        var allowed = Token("member");
        var denied = Token("non-member", expired);
        _authorizer.Allowed[allowed] = true;
        if (expired) _authorizer.Allowed[denied] = true; // Hub independently enforces expiry.
        using var cts = new CancellationTokenSource();
        await using var member = client.SubscribeAsync<TestPubSubMessage>("thread.typing", cts.Token, allowed).GetAsyncEnumerator();
        await using var nonMember = client.SubscribeAsync<TestPubSubMessage>("thread.typing", cts.Token, denied).GetAsyncEnumerator();
        var memberNext = member.MoveNextAsync().AsTask();
        var deniedNext = nonMember.MoveNextAsync().AsTask();
        try
        {
            await ReadSubscriptionsAsync(2);
            await BarrierAsync(client);
            await publisher.PublishAsync("thread.typing", new TestPubSubMessage(1, "authorized only"));
            (await memberNext.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
            await Task.Delay(100);
            deniedNext.IsCompleted.Should().BeFalse();

            // A reconnect must reauthorize; previous permission is not a local fan-out grant.
            _authorizer.Allowed.TryRemove(allowed, out _);
            memberNext = member.MoveNextAsync().AsTask();
            await client.GetPrimaryConnection().Transport.CloseAsync();
            await ReadSubscriptionsAsync(2);
            await BarrierAsync(client);
            await publisher.PublishAsync("thread.typing", new TestPubSubMessage(2, "no longer authorized"));
            await Task.Delay(100);
            memberNext.IsCompleted.Should().BeFalse();
            deniedNext.IsCompleted.Should().BeFalse();
        }
        finally
        {
            cts.Cancel();
            try { await memberNext; } catch (OperationCanceledException) { }
            try { await deniedNext; } catch (OperationCanceledException) { }
        }
    }

    private BoltClient CreateClient(string id) => new(_uri, id, id,
        new BoltClientOptions { MinConnections = 1, MaxConnections = 1, RpcTimeoutSeconds = 5 },
        NullLogger<BoltClient>.Instance);

    private static async Task BarrierAsync(BoltClient client)
    {
        var (status, _) = await client.InvokeAsync("_", "barrier", ReadOnlyMemory<byte>.Empty);
        status.Should().Be(HttpStatusCode.OK);
    }

    private async Task<BoltTopicAuthorizationContext[]> ReadSubscriptionsAsync(int count)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var contexts = new List<BoltTopicAuthorizationContext>();
        while (contexts.Count < count)
            contexts.Add(await _authorizer.Subscriptions.Reader.ReadAsync(timeout.Token));
        return contexts.ToArray();
    }

    private static string Token(string actor, bool expired = false)
    {
        var payload = JsonSerializer.Serialize(new
        {
            sub = actor,
            exp = DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 10).ToUnixTimeSeconds()
        });
        return "eyJhbGciOiJub25lIn0." + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".test";
    }

    private sealed class ActorTopicAuthorizer : IBoltTopicAuthorizer
    {
        public ConcurrentDictionary<string, bool> Allowed { get; } = new();
        public Channel<BoltTopicAuthorizationContext> Subscriptions { get; } = Channel.CreateUnbounded<BoltTopicAuthorizationContext>();

        public ValueTask<bool> AuthorizeAsync(BoltTopicAuthorizationContext context, CancellationToken ct = default)
        {
            if (context.Operation == BoltTopicOperation.Publish) return ValueTask.FromResult(context.ClientId == "publisher");
            if (context.Operation == BoltTopicOperation.Subscribe) Subscriptions.Writer.TryWrite(context);
            return ValueTask.FromResult(BoltTransientSubscriberId.IsScopedToClient(context.SubscriberId, context.ClientId) &&
                context.ActorAccessToken is { } token && Allowed.ContainsKey(token));
        }
    }
}
