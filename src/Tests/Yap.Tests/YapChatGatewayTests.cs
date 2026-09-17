using System.Buffers;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Bolt.Protocol;
using Communications.Integration.Drivers;
using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Clients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using XFramework.Integration.Security;
using Yap.Contracts;
using Yap.Services;

namespace Yap.Tests;

[TestFixture, CancelAfter(30000)]
public sealed class YapChatGatewayTests
{
    [Test]
    public async Task TypingSubscription_OnlyQueuesOtherParticipantsInTheWatchedTenantAndThread()
    {
        await using var fixture = await Fixture.CreateAsync();
        var tenant = Guid.Parse(fixture.Alice.FindFirstValue(YapAuth.TenantClaim)!);
        var actor = Guid.Parse(fixture.Alice.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var other = Guid.NewGuid(); var thread = Guid.NewGuid();
        Func<CommunicationsTypingState, Task>? publish = null;
        fixture.Wrapper.Setup(x => x.GetThreadAsync(It.IsAny<GetThreadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new GetThreadResponse { Id = thread, Members = [new() { CredentialId = actor }, new() { CredentialId = other }] }));
        fixture.Wrapper.Setup(x => x.SubscribeTypingAsync(tenant, thread, It.IsAny<Func<CommunicationsTypingState, Task>>(),
                It.IsAny<Func<CancellationToken, ValueTask<string?>>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Func<CommunicationsTypingState, Task>, Func<CancellationToken, ValueTask<string?>>, CancellationToken>(
                (_, _, handler, _, _) => publish = handler).Returns(Task.CompletedTask);
        using var cookies = await fixture.LoginAsync(fixture.Alice);
        var ticket = await fixture.Gateway.CreateTicketAsync(fixture.Alice, default);
        using var socket = fixture.Socket(cookies);
        await socket.ConnectAsync(fixture.Url(ticket), default);
        await RegisterAsync(socket, ticket.ClientId);
        await ReceiveAsync(socket); // Consume initial reconciliation before watching typing.
        Assert.That((await InvokeAsync(socket, ticket.ClientId, "watch", new { threadId = thread })).Status, Is.EqualTo(204));
        Assert.That(publish, Is.Not.Null);

        await publish!(new() { TenantId = tenant, ThreadId = thread, CredentialId = actor, IsTyping = true });
        await publish(new() { TenantId = Guid.NewGuid(), ThreadId = thread, CredentialId = other, IsTyping = true });
        await publish(new() { TenantId = tenant, ThreadId = Guid.NewGuid(), CredentialId = other, IsTyping = true });
        await publish(new() { TenantId = tenant, ThreadId = thread, CredentialId = other, IsTyping = true });

        var packet = await ReceiveAsync(socket);
        Assert.That(BoltCodec.TryReadRequest(packet, out var frame, out _), Is.True);
        var pushed = JsonSerializer.Deserialize<ChatSocketEvent>(frame.GetPayload(packet), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.That(pushed.Kind, Is.EqualTo("typing"));
        Assert.That(pushed.Body!.Value.Deserialize<TypingUpdate>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!.CredentialId, Is.EqualTo(other));
        // One lookup admits the watch; only the other participant's event needs a fresh
        // authorization lookup. Self echoes never consume the message projection queue.
        fixture.Wrapper.Verify(x => x.GetThreadAsync(It.IsAny<GetThreadRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "test complete", default);
    }

    [Test]
    public async Task QueuedDuplexEvents_ShareProjectionAndDirectoryLookupWithoutLosingCreationOrReceiptOrder()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var other = Guid.NewGuid(); var thread = Guid.NewGuid();
        var own = Guid.NewGuid(); var incoming = Guid.NewGuid(); var nextOwn = Guid.NewGuid();
        var first = MessageEvent(tenant, thread, actor, "MessageCreated", own);
        var queued = new[]
        {
            MessageEvent(tenant, thread, other, "MessageCreated", incoming),
            MessageEvent(tenant, thread, other, "MessagesDelivered", own),
            MessageEvent(tenant, thread, actor, "MessageCreated", nextOwn),
            MessageEvent(tenant, thread, other, "MessagesRead", own)
        };
        var session = new Mock<ICommunicationsChatSession>();
        session.SetupGet(x => x.CredentialId).Returns(actor);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queries = new List<List<Guid>>();
        session.Setup(x => x.GetMessageProjectionsAsync(thread, It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, List<Guid> ids, CancellationToken ct) =>
            {
                queries.Add(ids.ToList());
                if (queries.Count == 1) { entered.SetResult(); await release.Task.WaitAsync(ct); }
                return ChatFixture.Ok(new GetThreadMessagesResponse { Items = ids.Select(id => new ThreadMessageItemResponse
                    { Id = id, SenderCredentialId = id == incoming ? other : actor, Text = "ciphertext", DeliveredCount = 1, ReadCount = 1 }).ToList() });
            });
        var directory = new Mock<IChatDirectory>();
        directory.Setup(x => x.ResolveAsync(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ChatPerson>());
        var pending = Channel.CreateUnbounded<YapChatGateway.PendingEvent>();

        var firstProjection = YapChatGateway.ProjectMessageBatchAsync([(first, YapRealtime.Hint(first)!)], session.Object, directory.Object, default);
        await entered.Task;
        foreach (var update in queued) pending.Writer.TryWrite(new(update, 10));
        release.SetResult();
        await firstProjection;
        var head = (CommunicationsRealtimeEvent)(await pending.Reader.ReadAsync()).Value;
        var batch = YapChatGateway.TakeMessageBatch(head, YapRealtime.Hint(head)!, pending.Reader, tenant, actor, thread.ToString("N"), out var bytes);
        var pushed = await YapChatGateway.ProjectMessageBatchAsync(batch, session.Object, directory.Object, default);

        Assert.Multiple(() =>
        {
            Assert.That(queries, Has.Count.EqualTo(2));
            Assert.That(queries[1], Is.EquivalentTo(new[] { incoming, own, nextOwn }));
            Assert.That(bytes, Is.EqualTo(30));
            Assert.That(pushed.Select(x => x.EventId), Is.EqualTo(queued.Select(x => x.EventId)));
            Assert.That(pushed.Select(x => x.Kind), Is.EqualTo(new[] { "MessageCreated", "MessageUpdated", "MessageCreated", "MessageUpdated" }));
            Assert.That(pushed.Select(x => x.ActorId), Is.EqualTo(new Guid?[] { other, null, actor, null }));
            Assert.That(pushed.Select(x => x.Messages!.Single().Id), Is.EqualTo(new[] { incoming, own, nextOwn, own }));
        });
        directory.Verify(x => x.ResolveAsync(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestCase("typing")]
    [TestCase("call")]
    [TestCase("refresh")]
    [TestCase("other-thread")]
    [TestCase("other-tenant")]
    public void MessageBatch_DoesNotCrossOrderingOrAuthorizationBarriers(string kind)
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var thread = Guid.NewGuid();
        var first = MessageEvent(tenant, thread, actor, "MessageCreated", Guid.NewGuid());
        object barrier = kind switch
        {
            "other-thread" => MessageEvent(tenant, Guid.NewGuid(), actor, "MessageCreated", Guid.NewGuid()),
            "other-tenant" => MessageEvent(Guid.NewGuid(), thread, actor, "MessageCreated", Guid.NewGuid()),
            _ => new ChatSocketEvent(Guid.NewGuid(), 0, kind)
        };
        var pending = Channel.CreateUnbounded<YapChatGateway.PendingEvent>();
        pending.Writer.TryWrite(new(barrier, 10));
        pending.Writer.TryWrite(new(MessageEvent(tenant, thread, actor, "MessageCreated", Guid.NewGuid()), 10));

        var batch = YapChatGateway.TakeMessageBatch(first, YapRealtime.Hint(first)!, pending.Reader, tenant, actor, null, out var bytes);

        Assert.Multiple(() => { Assert.That(batch, Has.Count.EqualTo(1)); Assert.That(bytes, Is.Zero); Assert.That(pending.Reader.TryPeek(out var remaining) && ReferenceEquals(remaining.Value, barrier), Is.True); });
    }

    [TestCase(1, 32)]
    [TestCase(3, 16)]
    public void MessageBatch_BoundsEventAndDistinctMessageCounts(int idsPerEvent, int expectedEvents)
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var thread = Guid.NewGuid();
        CommunicationsRealtimeEvent Create() => MessageEvent(tenant, thread, actor, "MessagesRead", Enumerable.Range(0, idsPerEvent).Select(_ => Guid.NewGuid()).ToArray());
        var first = Create(); var pending = Channel.CreateUnbounded<YapChatGateway.PendingEvent>();
        for (var i = 0; i < 40; i++) pending.Writer.TryWrite(new(Create(), 10));

        var batch = YapChatGateway.TakeMessageBatch(first, YapRealtime.Hint(first)!, pending.Reader, tenant, actor, null, out _);

        Assert.That(batch, Has.Count.EqualTo(expectedEvents));
        Assert.That(pending.Reader.TryPeek(out _), Is.True);
    }

    [Test]
    public async Task MessageBatch_MissingAndReplyProjectionsReconcileOnlyAffectedEvents()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var thread = Guid.NewGuid();
        var valid = Guid.NewGuid(); var missing = Guid.NewGuid(); var reply = Guid.NewGuid();
        var events = new[] { valid, missing, reply }.Select(id => MessageEvent(tenant, thread, actor, "MessageCreated", id)).ToArray();
        var session = new Mock<ICommunicationsChatSession>();
        session.SetupGet(x => x.CredentialId).Returns(actor);
        session.Setup(x => x.GetMessageProjectionsAsync(thread, It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new GetThreadMessagesResponse { Items = [new() { Id = valid }, new() { Id = reply, IsThreadReply = true }] }));
        var directory = new Mock<IChatDirectory>();
        directory.Setup(x => x.ResolveAsync(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ChatPerson>());

        var pushed = await YapChatGateway.ProjectMessageBatchAsync(events.Select(x => (x, YapRealtime.Hint(x)!)).ToList(), session.Object, directory.Object, default);

        Assert.That(pushed.Select(x => x.Kind), Is.EqualTo(new[] { "MessageCreated", "refresh", "refresh" }));
        Assert.That(pushed.Skip(1).All(x => x.Messages is null && x.ActorId is null), Is.True);
    }

    private static CommunicationsRealtimeEvent MessageEvent(Guid tenant, Guid thread, Guid actor, string kind, params Guid[] ids) => new()
    {
        EventId = Guid.NewGuid(), TenantId = tenant, ThreadId = thread, ActorCredentialId = actor,
        EventType = kind, PayloadJson = JsonSerializer.Serialize(new { messageIds = ids })
    };

    [TestCase(3600, 300)]
    [TestCase(80, 65)]
    [TestCase(10, 0)]
    public void SubscriptionLifetime_OnlyShortensRebindBeforeTrustedTokenExpiry(int expiresIn, int expectedSeconds)
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1800000000);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(
            new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(expires: now.AddSeconds(expiresIn).UtcDateTime));
        Assert.That(YapChatGateway.SubscriptionLifetime(token, now), Is.EqualTo(TimeSpan.FromSeconds(expectedSeconds)));
        Assert.That(YapChatGateway.SubscriptionLifetime("opaque-fixture-token", now), Is.EqualTo(TimeSpan.FromMinutes(5)));
    }

    [Test]
    public async Task Ticket_RequiresCurrentSessionAndIsBoundToAccountAndOneUpgrade()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var alice = await fixture.LoginAsync(fixture.Alice);
        using var bob = await fixture.LoginAsync(fixture.Bob);
        var ticket = await fixture.Gateway.CreateTicketAsync(fixture.Alice, default);
        using var wrong = fixture.Socket(bob);
        Assert.ThrowsAsync<WebSocketException>(() => wrong.ConnectAsync(fixture.Url(ticket), default));
        using var accepted = fixture.Socket(alice);
        await accepted.ConnectAsync(fixture.Url(ticket), default);
        await RegisterAsync(accepted, ticket.ClientId);
        using var replay = fixture.Socket(alice);
        Assert.ThrowsAsync<WebSocketException>(() => replay.ConnectAsync(fixture.Url(ticket), default));
        await accepted.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "test complete", default);

        fixture.Revoke(fixture.Alice);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => fixture.Gateway.CreateTicketAsync(fixture.Alice, default))!.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task Socket_RejectsCrossOriginAndRevokedTicket()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cookies = await fixture.LoginAsync(fixture.Alice);
        var ticket = await fixture.Gateway.CreateTicketAsync(fixture.Alice, default);
        using var foreign = fixture.Socket(cookies, "https://another.example");
        Assert.ThrowsAsync<WebSocketException>(() => foreign.ConnectAsync(fixture.Url(ticket), default));
        fixture.Revoke(fixture.Alice);
        using var revoked = fixture.Socket(cookies);
        Assert.ThrowsAsync<WebSocketException>(() => revoked.ConnectAsync(fixture.Url(ticket), default));
    }

    [Test]
    public async Task ConnectedRpc_RejectsUnknownOperationAndRechecksSessionRevocation()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cookies = await fixture.LoginAsync(fixture.Alice);
        var ticket = await fixture.Gateway.CreateTicketAsync(fixture.Alice, default);
        using var socket = fixture.Socket(cookies);
        await socket.ConnectAsync(fixture.Url(ticket), default);
        await RegisterAsync(socket, ticket.ClientId);
        Assert.That((await InvokeAsync(socket, ticket.ClientId, "internal-route")).Status, Is.EqualTo(400));
        fixture.Revoke(fixture.Alice);
        Assert.That((await InvokeAsync(socket, ticket.ClientId, "send")).Status, Is.EqualTo(401));
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "test complete", default);
    }

    [Test]
    public async Task Send_BackendConnectionLostReturnsRetryableStatus()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Wrapper.Setup(x => x.CreateThreadMessageAsync(It.IsAny<CreateThreadMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection lost"));
        using var cookies = await fixture.LoginAsync(fixture.Alice);
        var ticket = await fixture.Gateway.CreateTicketAsync(fixture.Alice, default);
        using var socket = fixture.Socket(cookies);
        await socket.ConnectAsync(fixture.Url(ticket), default);
        await RegisterAsync(socket, ticket.ClientId);

        var response = await InvokeAsync(socket, ticket.ClientId, "send", new SendMessage(Guid.NewGuid(), Guid.NewGuid(), "test"));

        Assert.That(response.Status, Is.EqualTo(503));
        fixture.Wrapper.Verify(x => x.CreateThreadMessageAsync(It.IsAny<CreateThreadMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "test complete", default);
    }

    [TestCase("send", "[]")]
    [TestCase("send", "{\"id\":42}")]
    [TestCase("ack", "{}")]
    [TestCase("ack", "{\"sequence\":\"1\"}")]
    [TestCase("ack", "{\"sequence\":1.5}")]
    [TestCase("watch", "{}")]
    [TestCase("watch", "{\"threadId\":42}")]
    [TestCase("watch", "{\"threadId\":\"invalid\"}")]
    public async Task MalformedCommand_RemainsBadRequest(string operation, string json)
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cookies = await fixture.LoginAsync(fixture.Alice);
        var ticket = await fixture.Gateway.CreateTicketAsync(fixture.Alice, default);
        using var socket = fixture.Socket(cookies);
        await socket.ConnectAsync(fixture.Url(ticket), default);
        await RegisterAsync(socket, ticket.ClientId);

        Assert.That((await InvokeAsync(socket, ticket.ClientId, operation, JsonSerializer.Deserialize<JsonElement>(json))).Status, Is.EqualTo(400));
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "test complete", default);
    }

    [Test]
    public async Task RevokedSessionDuringPush_EndsSocketWithoutEscapingIntoHttpErrorHandling()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cookies = await fixture.LoginAsync(fixture.Alice);
        var ticket = await fixture.Gateway.CreateTicketAsync(fixture.Alice, default);
        using var socket = fixture.Socket(cookies);
        await socket.ConnectAsync(fixture.Url(ticket), default);
        await RegisterAsync(socket, ticket.ClientId);
        var publish = await fixture.Subscriptions.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));

        fixture.Revoke(fixture.Alice);
        await publish(new CommunicationsRealtimeEvent
        {
            EventId = Guid.NewGuid(), TenantId = Guid.Parse(fixture.Alice.FindFirstValue(YapAuth.TenantClaim)!),
            EventType = "ThreadChanged"
        });

        var escaped = await fixture.CompletedUpgrades.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(escaped, Is.False, "Post-upgrade failures must not reach an HTTP problem response writer.");
    }

    [Test]
    public async Task SlowConsumer_BacklogClosesConnectionAndReconnectStartsWithExplicitReconciliation()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cookies = await fixture.LoginAsync(fixture.Alice);
        var ticket = await fixture.Gateway.CreateTicketAsync(fixture.Alice, default);
        using var socket = fixture.Socket(cookies);
        await socket.ConnectAsync(fixture.Url(ticket), default);
        await RegisterAsync(socket, ticket.ClientId);
        var publish = await fixture.Subscriptions.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        for (var i = 0; i < 260; i++) await publish(new CommunicationsRealtimeEvent
        {
            EventId = Guid.NewGuid(), TenantId = Guid.Parse(fixture.Alice.FindFirstValue(YapAuth.TenantClaim)!),
            EventType = "ThreadChanged"
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var buffer = new byte[64 * 1024];
            while ((await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token)).MessageType != WebSocketMessageType.Close) { }
        }
        catch (WebSocketException) { /* An abort also forces explicit catch-up on the next socket. */ }

        ticket = await fixture.Gateway.CreateTicketAsync(fixture.Alice, default);
        using var resumed = fixture.Socket(cookies);
        await resumed.ConnectAsync(fixture.Url(ticket), default);
        await RegisterAsync(resumed, ticket.ClientId);
        var refresh = await ReceiveAsync(resumed);
        Assert.That(refresh[0], Is.EqualTo((byte)FrameType.Push));
        Assert.That(BoltCodec.TryReadRequest(refresh, out var frame, out _), Is.True);
        var value = JsonSerializer.Deserialize<ChatSocketEvent>(frame.GetPayload(refresh), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Multiple(() => { Assert.That(value.Kind, Is.EqualTo("refresh")); Assert.That(value.Sequence, Is.EqualTo(1)); });
        await resumed.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "test complete", default);
    }

    private static async Task RegisterAsync(ClientWebSocket socket, string clientId)
    {
        var writer = new ArrayBufferWriter<byte>();
        BoltCodec.WriteRegister(writer, clientId, "yap-browser");
        await socket.SendAsync(writer.WrittenMemory, WebSocketMessageType.Binary, true, default);
        var response = await ReceiveAsync(socket);
        Assert.That(BoltCodec.TryReadRegisterAck(response, out var accepted, out _), Is.True);
        Assert.That(accepted, Is.True);
    }

    private static async Task<ChatSocketResponse> InvokeAsync(ClientWebSocket socket, string clientId, string operation, object? body = null)
    {
        var id = Guid.NewGuid();
        var writer = new ArrayBufferWriter<byte>();
        BoltCodec.WriteRequest(writer, id, BoltCodec.Fnv1aHash("yap"), BoltCodec.Fnv1aHash(clientId), BoltCodec.Fnv1aHash("yap.chat"),
            JsonSerializer.SerializeToUtf8Bytes(new { operation, body = body ?? new { } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await socket.SendAsync(writer.WrittenMemory, WebSocketMessageType.Binary, true, default);
        while (true)
        {
            var packet = await ReceiveAsync(socket);
            var frames = new List<byte[]>();
            if (BoltCodec.TryReadBatch(packet, out var batch))
                foreach (var frame in batch) frames.Add(frame.ToArray());
            else frames.Add(packet);
            foreach (var bytes in frames)
                if (bytes[0] == (byte)FrameType.Response && BoltCodec.TryReadResponse(bytes, out var response, out _) && response.RequestId == id)
                    return JsonSerializer.Deserialize<ChatSocketResponse>(response.GetPayload(bytes), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
    }

    private static async Task<byte[]> ReceiveAsync(ClientWebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[64 * 1024];
        var result = await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token);
        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Binary));
        Assert.That(result.EndOfMessage, Is.True);
        return buffer[..result.Count];
    }

    private sealed class Fixture(WebApplication app) : IAsyncDisposable
    {
        public ClaimsPrincipal Alice { get; private set; } = null!;
        public ClaimsPrincipal Bob { get; private set; } = null!;
        public YapChatGateway Gateway => app.Services.GetRequiredService<YapChatGateway>();
        public Mock<ICommunicationsServiceWrapper> Wrapper { get; private set; } = null!;
        public Channel<Func<CommunicationsRealtimeEvent, Task>> Subscriptions { get; } = Channel.CreateUnbounded<Func<CommunicationsRealtimeEvent, Task>>();
        public Channel<bool> CompletedUpgrades { get; } = Channel.CreateUnbounded<bool>();
        private Uri origin = null!;
        public static async Task<Fixture> CreateAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Configuration["Yap:Encryption:Enabled"] = "false";
            builder.Services.AddAuthentication(YapAuth.Scheme).AddCookie(YapAuth.Scheme);
            builder.Services.AddAuthorization();
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddDataProtection();
            var wrapper = new Mock<ICommunicationsServiceWrapper>();
            builder.Services.AddSingleton(wrapper.Object);
            builder.Services.AddSingleton(Mock.Of<IActorAccessTokenScope>());
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<YapSessions>();
            builder.Services.AddSingleton<YapCallGateway>();
            builder.Services.AddSingleton<YapChatGateway>();
            var app = builder.Build();
            var fixture = new Fixture(app) { Wrapper = wrapper };
            wrapper.Setup(x => x.SubscribeLiveUserCommunicationsEventsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Func<CommunicationsRealtimeEvent, Task>>(), It.IsAny<Func<CancellationToken, ValueTask<string?>>>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, Guid, Func<CommunicationsRealtimeEvent, Task>, Func<CancellationToken, ValueTask<string?>>, CancellationToken>(
                    (_, _, handler, _, _) => fixture.Subscriptions.Writer.TryWrite(handler))
                .Returns(Task.CompletedTask);
            var sessions = app.Services.GetRequiredService<YapSessions>();
            fixture.Alice = await sessions.CreateAsync(YapSessionsTests.Session());
            fixture.Bob = await sessions.CreateAsync(YapSessionsTests.Session());
            // Terminated TLS is simulated locally; the gateway still validates scheme+Origin.
            app.Use((context, next) => { context.Request.Scheme = "https"; return next(context); });
            app.UseAuthentication();
            app.UseWebSockets();
            app.UseAuthorization();
            app.MapPost("/test-login/{name}", async (string name, HttpContext context) =>
                await context.SignInAsync(YapAuth.Scheme, name == "alice" ? fixture.Alice : fixture.Bob));
            app.MapGet("/api/chat/socket", async (HttpContext context, YapChatGateway gateway) =>
            {
                var escaped = false;
                try { await gateway.AcceptSocketAsync(context); }
                catch (YapApiException error) when (!context.Response.HasStarted) { context.Response.StatusCode = error.Status; }
                catch (Exception) when (context.Response.HasStarted) { escaped = true; throw; }
                finally
                {
                    if (context.Response.HasStarted) fixture.CompletedUpgrades.Writer.TryWrite(escaped);
                }
            }).RequireAuthorization();
            await app.StartAsync();
            fixture.origin = new Uri(app.Urls.Single());
            return fixture;
        }
        public async Task<HttpClientHandler> LoginAsync(ClaimsPrincipal user)
        {
            var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
            using var client = new HttpClient(handler, disposeHandler: false);
            using var response = await client.PostAsync(new Uri(origin, user == Alice ? "/test-login/alice" : "/test-login/bob"), null);
            response.EnsureSuccessStatusCode();
            return handler;
        }
        public ClientWebSocket Socket(HttpClientHandler client, string? explicitOrigin = null)
        {
            var socket = new ClientWebSocket();
            // Test cookies are Secure because the app sees TLS; pass them on the local test socket explicitly.
            socket.Options.SetRequestHeader("Cookie", client.CookieContainer.GetCookieHeader(new UriBuilder(origin) { Scheme = "https" }.Uri));
            socket.Options.SetRequestHeader("Origin", explicitOrigin ?? $"https://{origin.Authority}");
            return socket;
        }
        public Uri Url(ChatSocketTicket ticket) => new UriBuilder(new Uri(origin, ticket.Url)) { Scheme = "ws" }.Uri;
        public void Revoke(ClaimsPrincipal user) => app.Services.GetRequiredService<IDistributedCache>().Remove($"yap:session:{user.FindFirstValue(YapAuth.SessionClaim)}");
        public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
    }
}
