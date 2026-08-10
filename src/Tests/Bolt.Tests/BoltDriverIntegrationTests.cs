using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using Bolt.Client;
using Bolt.Domain.Shared.Contracts.Requests;
using Bolt.Server;
using FluentAssertions;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(20000)]
public class BoltDriverIntegrationTests
{
    private WebApplication _serverApp = null!;
    private Uri _serverUri = null!;
    private ILoggerFactory _loggerFactory = null!;
    private static int _portCounter = 19600;
    private int _port;

    [SetUp]
    public async Task SetUp()
    {
        _port = Interlocked.Increment(ref _portCounter);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_port}");
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        _serverApp = builder.Build();
        _serverApp.UseWebSockets();
        _serverApp.MapBolt("/bolt");
        _serverApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _serverApp.RunAsync());
        await WaitForHealth($"http://localhost:{_port}/health");
        _serverUri = new Uri($"ws://localhost:{_port}/bolt");
        _loggerFactory = _serverApp.Services.GetRequiredService<ILoggerFactory>();
    }

    [TearDown]
    public async Task TearDown()
    {
        try { await _serverApp.StopAsync(); } catch { }
        try { await _serverApp.DisposeAsync(); } catch { }
    }

    [Test]
    public async Task SendAsync_ServiceNameRecipient_RoutesToDeterministicClientId()
    {
        await using var caller = CreateClient("driver-caller", "DriverCaller");
        await using var target = CreateClient(XFrameworkServiceNames.IdentityServer.ToSha256(), XFrameworkServiceNames.IdentityServer);
        var tokenProvider = new RecordingServiceTokenProvider();
        var driver = CreateDriver(caller, tokenProvider);

        target.RegisterHandler(nameof(BoltDriverTestRequest), (payload, _) =>
        {
            var envelope = MemoryPackSerializer.Deserialize<BoltInvocationEnvelope>(payload.Span);
            var request = MemoryPackSerializer.Deserialize<BoltDriverTestRequest>(envelope!.Payload);
            request.Should().NotBeNull();
            envelope.ServiceAccessToken.Should().Be("service-token");

            var response = new QueryResponse<BoltDriverTestResponse>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "OK",
                Response = new BoltDriverTestResponse { Text = request!.Text }
            };
            return Task.FromResult((HttpStatusCode.OK, (ReadOnlyMemory<byte>)MemoryPackSerializer.Serialize(response)));
        });

        await target.ConnectAsync();
        await caller.ConnectAsync();

        var result = await driver.SendAsync<BoltDriverTestRequest, BoltDriverTestResponse>(
            new BoltDriverTestRequest { Text = "resolved" },
            XFrameworkServiceNames.IdentityServer);

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.Response!.Text.Should().Be("resolved");
        tokenProvider.Audiences.Should().ContainSingle().Which.Should().Be(XFrameworkServiceNames.IdentityServer);
    }

    [Test]
    public async Task SendAsync_MatchingTrustedServiceContext_ReusesAuthorizedToken()
    {
        await using var caller = CreateClient("driver-trusted-caller", "DriverTrustedCaller");
        await using var target = CreateClient(XFrameworkServiceNames.Storage.ToSha256(), XFrameworkServiceNames.Storage);
        var fallbackProvider = new RecordingServiceTokenProvider();
        var scopes = new HashSet<string>(
            [XFrameworkServiceScopes.StorageWrite, XFrameworkServiceScopes.TenantTarget],
            StringComparer.OrdinalIgnoreCase);
        var context = new TrustedInvocationContext(
            null,
            new TrustedServiceIdentity(
                XFrameworkServiceNames.IdentityServer,
                XFrameworkServiceNames.Storage,
                scopes,
                "generation"),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        var driver = CreateDriver(
            caller,
            fallbackProvider,
            new FixedTrustedInvocationContextAccessor(context),
            new FixedTrustedServiceAccessTokenAccessor(new TrustedServiceAccessToken(
                "authorized-token",
                XFrameworkServiceNames.IdentityServer,
                XFrameworkServiceNames.Storage,
                scopes)));

        target.RegisterHandler(nameof(BoltDriverTestRequest), (payload, _) =>
        {
            var envelope = MemoryPackSerializer.Deserialize<BoltInvocationEnvelope>(payload.Span);
            envelope!.ServiceAccessToken.Should().Be("authorized-token");
            return Task.FromResult((HttpStatusCode.OK, (ReadOnlyMemory<byte>)MemoryPackSerializer.Serialize(
                new QueryResponse<BoltDriverTestResponse>
                {
                    HttpStatusCode = HttpStatusCode.OK,
                    Response = new BoltDriverTestResponse { Text = "trusted" }
                })));
        });

        await target.ConnectAsync();
        await caller.ConnectAsync();
        var result = await driver.SendAsync<BoltDriverTestRequest, BoltDriverTestResponse>(
            new BoltDriverTestRequest(),
            XFrameworkServiceNames.Storage);

        result.Response!.Text.Should().Be("trusted");
        fallbackProvider.Audiences.Should().BeEmpty();
    }

    [Test]
    public async Task Unsubscribe_CancelsLegacySubscriptionAndStopsDelivery()
    {
        await using var subscriber = CreateClient("driver-subscriber", "DriverSubscriber");
        await using var publisher = CreateClient("driver-publisher", "DriverPublisher");
        var driver = CreateDriver(subscriber, new RecordingServiceTokenProvider());
        var topic = $"driver.unsubscribe.{Guid.NewGuid():N}";
        var received = new ConcurrentQueue<TestPubSubMessage>();
        var firstMessage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await subscriber.ConnectAsync();
        await publisher.ConnectAsync();

        await driver.Subscribe(new BoltSubscriptionRequest<TestPubSubMessage>(topic, message =>
        {
            received.Enqueue(message);
            firstMessage.TrySetResult();
        }));
        await Task.Delay(300);

        await publisher.PublishAsync(topic, new TestPubSubMessage(1, "before"));
        await firstMessage.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await driver.Unsubscribe(new BoltSubscriptionRequest { Name = topic });
        await Task.Delay(300);
        await publisher.PublishAsync(topic, new TestPubSubMessage(2, "after"));
        await Task.Delay(500);

        received.Should().ContainSingle();
        received.TryPeek(out var message).Should().BeTrue();
        message!.Id.Should().Be(1);
    }

    [Test]
    public async Task Dispose_CancelsTransientSubscriptionAndWaitsForActiveHandler()
    {
        await using var subscriber = CreateClient("driver-transient-dispose", "DriverTransientDispose");
        await using var publisher = CreateClient("driver-transient-publisher", "DriverTransientPublisher");
        var driver = CreateDriver(subscriber, new RecordingServiceTokenProvider());
        var topic = $"driver.transient.dispose.{Guid.NewGuid():N}";
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = 0;

        await subscriber.ConnectAsync();
        await publisher.ConnectAsync();
        await driver.SubscribeAsync<TestPubSubMessage>(topic, async _ =>
        {
            Interlocked.Increment(ref received);
            handlerStarted.TrySetResult();
            await releaseHandler.Task;
        });
        await Task.Delay(300);

        await publisher.PublishAsync(topic, new TestPubSubMessage(1, "before"));
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var disposeTask = Task.Run(driver.Dispose);
        await Task.Delay(100);
        disposeTask.IsCompleted.Should().BeFalse(
            "the scoped driver must not be released while its handler still uses scoped dependencies");

        releaseHandler.TrySetResult();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await publisher.PublishAsync(topic, new TestPubSubMessage(2, "after"));
        await Task.Delay(300);

        received.Should().Be(1);
    }

    [Test]
    public async Task Dispose_CancelsDurableSubscriptionAndWaitsForActiveHandler()
    {
        await using var subscriber = CreateClient("driver-durable-dispose", "DriverDurableDispose");
        await using var publisher = CreateClient("driver-durable-publisher", "DriverDurablePublisher");
        var driver = CreateDriver(subscriber, new RecordingServiceTokenProvider());
        var topic = $"driver.durable.dispose.{Guid.NewGuid():N}";
        var subscriberId = $"driver-durable-{Guid.NewGuid():N}";
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = 0;

        await subscriber.ConnectAsync();
        await publisher.ConnectAsync();
        await driver.SubscribeDurableAsync<TestPubSubMessage>(topic, subscriberId, async _ =>
        {
            Interlocked.Increment(ref received);
            handlerStarted.TrySetResult();
            await releaseHandler.Task;
        });
        await Task.Delay(300);

        await publisher.PublishAsync(topic, new TestPubSubMessage(1, "before"), durable: true);
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var disposeTask = Task.Run(driver.Dispose);
        await Task.Delay(100);
        disposeTask.IsCompleted.Should().BeFalse(
            "the scoped driver must not be released while its durable handler still uses scoped dependencies");

        releaseHandler.TrySetResult();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await publisher.PublishAsync(topic, new TestPubSubMessage(2, "after"), durable: true);
        await Task.Delay(300);

        received.Should().Be(1);
    }

    [Test]
    public async Task CloseAsync_RemovesLocallyTrackedOutboundStream()
    {
        await using var sender = CreateClient("stream-cleanup-a", "StreamCleanupA");
        await using var receiver = CreateClient("stream-cleanup-b", "StreamCleanupB");
        var streamAccepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.RegisterStreamHandler("unused-stream", async _ =>
        {
            streamAccepted.TrySetResult();
            await releaseHandler.Task;
        });

        await receiver.ConnectAsync();
        await sender.ConnectAsync();

        try
        {
            var stream = await sender.OpenStreamAsync("stream-cleanup-b", "unused-stream");
            await streamAccepted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            GetActiveStreamCount(sender).Should().Be(1);

            await stream.CloseAsync();

            GetActiveStreamCount(sender).Should().Be(0);
        }
        finally
        {
            releaseHandler.TrySetResult();
        }
    }

    [Test]
    public async Task BoltDriver_BridgesBoltClientLifecycleCallbacksOnReconnect()
    {
        await using var client = CreateClient("lifecycle-client", "LifecycleClient");
        var driver = CreateDriver(client, new RecordingServiceTokenProvider());
        var callbacks = new ConcurrentQueue<string>();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reconnecting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        driver.OnDisconnected = () =>
        {
            callbacks.Enqueue("disconnected");
            disconnected.TrySetResult();
        };
        driver.OnReconnecting = () =>
        {
            callbacks.Enqueue("reconnecting");
            reconnecting.TrySetResult();
        };
        driver.OnReconnected = () =>
        {
            callbacks.Enqueue("reconnected");
            reconnected.TrySetResult();
        };

        await client.ConnectAsync();
        await client.GetPrimaryConnection().Transport.CloseAsync();

        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await reconnecting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        callbacks.Should().Equal("disconnected", "reconnecting", "reconnected");
    }

    private BoltClient CreateClient(string id, string name) =>
        new(_serverUri, id, name, new BoltClientOptions { RpcTimeoutSeconds = 5 },
            _loggerFactory.CreateLogger<BoltClient>());

    private static BoltDriver CreateDriver(
        BoltClient client,
        IServiceTokenProvider tokenProvider,
        ITrustedInvocationContextAccessor? invocationContextAccessor = null,
        ITrustedServiceAccessTokenAccessor? serviceAccessTokenAccessor = null)
        => new(
            client,
            Options.Create(new BoltConfiguration
            {
                ClientName = XFrameworkServiceNames.Portal,
                ClientGuid = Guid.NewGuid()
            }),
            tokenProvider,
            new NullActorAccessTokenProvider(),
            invocationContextAccessor ?? new FixedTrustedInvocationContextAccessor(null),
            serviceAccessTokenAccessor ?? new FixedTrustedServiceAccessTokenAccessor(null),
            NullLogger<BoltDriver>.Instance);

    private static int GetActiveStreamCount(BoltClient client)
    {
        var field = typeof(BoltClient).GetField("_activeStreams", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        var streams = (ConcurrentDictionary<Guid, BoltStream>)field!.GetValue(client)!;
        return streams.Count;
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try { if ((await client.GetAsync(url)).IsSuccessStatusCode) return; } catch { }
            await Task.Delay(100);
        }
        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }

    private sealed class RecordingServiceTokenProvider : IServiceTokenProvider
    {
        public ConcurrentQueue<string> Audiences { get; } = new();

        public ValueTask<string> GetTokenAsync(
            string audience,
            IReadOnlyCollection<string>? scopes = null,
            CancellationToken ct = default)
        {
            Audiences.Enqueue(audience);
            return ValueTask.FromResult("service-token");
        }
    }

    private sealed class NullActorAccessTokenProvider : IActorAccessTokenProvider
    {
        public ValueTask<string?> GetTokenAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<string?>(null);
    }

    private sealed class FixedTrustedInvocationContextAccessor(TrustedInvocationContext? current)
        : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext? Current { get; } = current;
    }

    private sealed class FixedTrustedServiceAccessTokenAccessor(TrustedServiceAccessToken? current)
        : ITrustedServiceAccessTokenAccessor
    {
        public TrustedServiceAccessToken? Current { get; } = current;
    }
}

[MemoryPackable]
public partial class BoltDriverTestRequest : IHasRequestServer
{
    public string Text { get; set; } = string.Empty;
    public RequestMetadata? Metadata { get; set; }
}

[MemoryPackable]
public partial class BoltDriverTestResponse
{
    public string Text { get; set; } = string.Empty;
}
