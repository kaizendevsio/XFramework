using System.Buffers;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Bolt.Protocol;
using Communications.Integration.Drivers;
using Communications.Domain.Shared.Contracts.Realtime;
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

    private static async Task<ChatSocketResponse> InvokeAsync(ClientWebSocket socket, string clientId, string operation)
    {
        var id = Guid.NewGuid();
        var writer = new ArrayBufferWriter<byte>();
        BoltCodec.WriteRequest(writer, id, BoltCodec.Fnv1aHash("yap"), BoltCodec.Fnv1aHash(clientId), BoltCodec.Fnv1aHash("yap.chat"),
            JsonSerializer.SerializeToUtf8Bytes(new { operation, body = new { } }));
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
        public Channel<Func<CommunicationsRealtimeEvent, Task>> Subscriptions { get; } = Channel.CreateUnbounded<Func<CommunicationsRealtimeEvent, Task>>();
        private Uri origin = null!;
        public static async Task<Fixture> CreateAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddAuthentication(YapAuth.Scheme).AddCookie(YapAuth.Scheme);
            builder.Services.AddAuthorization();
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddDataProtection();
            var wrapper = new Mock<ICommunicationsServiceWrapper>();
            builder.Services.AddSingleton(wrapper.Object);
            builder.Services.AddSingleton(Mock.Of<IActorAccessTokenScope>());
            builder.Services.AddSingleton<YapSessions>();
            builder.Services.AddSingleton<YapCallGateway>();
            builder.Services.AddSingleton<YapChatGateway>();
            var app = builder.Build();
            var fixture = new Fixture(app);
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
                try { await gateway.AcceptSocketAsync(context); }
                catch (YapApiException error) when (!context.Response.HasStarted) { context.Response.StatusCode = error.Status; }
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
