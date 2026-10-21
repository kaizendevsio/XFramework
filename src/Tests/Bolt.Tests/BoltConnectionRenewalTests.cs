using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(30000)]
public sealed class BoltConnectionRenewalTests
{
    private readonly ConcurrentDictionary<string, ClaimsPrincipal> _tickets = new();
    private WebApplication _app = null!;
    private Uri _uri = null!;

    [SetUp]
    public async Task SetUp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddBoltServer(options =>
        {
            options.RegistrationIdentityBindingMode = BoltRegistrationIdentityBindingMode.Enforce;
            options.InvocationTimeoutMs = 15000;
            options.MaxConnectionsPerPrincipal = 6;
        });
        _app = builder.Build();
        _app.Use(async (context, next) =>
        {
            var bearer = context.Request.Headers.Authorization.ToString();
            if (!bearer.StartsWith("Bearer ", StringComparison.Ordinal) ||
                !_tickets.TryGetValue(bearer[7..], out var principal) ||
                long.Parse(principal.FindFirst("exp")!.Value, CultureInfo.InvariantCulture) <=
                DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            // Ticket issuance is controlled by this fixture; production JWT validation is
            // covered separately by BoltTransportAuthenticationHandshakeTests.
            context.User = principal;
            await next(context);
        });
        _app.UseWebSockets();
        _app.MapBolt("/bolt/ws");
        await _app.StartAsync();
        _uri = new Uri(_app.Urls.Single().Replace("http://", "ws://") + "/bolt/ws");
    }

    [TearDown]
    public async Task TearDown()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _tickets.Clear();
    }

    [TestCase(true, 16)]
    [TestCase(false, 16)]
    [TestCase(true, 4096)]
    [TestCase(false, 4096)]
    public async Task Renewal_InFlightMutation_CompletesOnceOnOriginalConnection(bool renewCaller, int payloadBytes)
    {
        await using var caller = CreateClient("caller", renewCaller);
        await using var receiver = CreateClient("receiver", !renewCaller);
        var entered = NewSignal();
        var release = NewSignal();
        var renewed = NewSignal();
        var mutationCount = 0;
        receiver.RegisterHandler("save", async (ReadOnlyMemory<byte> payload, Guid _, CancellationToken ct) =>
        {
            Interlocked.Increment(ref mutationCount);
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return (HttpStatusCode.OK, payload);
        });
        receiver.RegisterHandler("ping", (_, _) => Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty)));
        var renewing = renewCaller ? caller : receiver;
        renewing.Reconnected += () => renewed.TrySetResult();
        await caller.ConnectAsync();
        await receiver.ConnectAsync();
        var original = renewing.GetPrimaryConnection();
        var payload = Enumerable.Repeat((byte)42, payloadBytes).ToArray();

        var save = caller.InvokeAsync(ClientId("receiver"), "save", payload);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await renewed.Task.WaitAsync(TimeSpan.FromSeconds(8));
            renewing.GetPrimaryConnection().Should().NotBeSameAs(original);
            original.Transport.IsConnected.Should().BeTrue("the accepted mutation is still draining");
            save.IsCompleted.Should().BeFalse();
            (await caller.InvokeAsync(ClientId("receiver"), "ping", ReadOnlyMemory<byte>.Empty))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            release.TrySetResult();

            var result = await save.WaitAsync(TimeSpan.FromSeconds(3));
            result.StatusCode.Should().Be(HttpStatusCode.OK);
            result.Data.ToArray().Should().Equal(payload);
            mutationCount.Should().Be(1, "renewal must not replay a mutation with an unknown outcome");
            await WaitUntilAsync(() => !original.Transport.IsConnected, seconds: 5);
            renewing.IsConnected.Should().BeTrue();
            renewing.GetHealthSnapshot().TotalUnexpectedDisconnects.Should().Be(0);
        }
        finally { release.TrySetResult(); }
    }

    [Test]
    public async Task Renewal_TransientSubscription_RebindsWithoutDuplicateDeliveryFromDrainingSocket()
    {
        await using var subscriber = CreateClient("subscriber", true);
        await using var publisher = CreateClient("publisher", false);
        var renewed = NewSignal();
        subscriber.Reconnected += () => renewed.TrySetResult();
        await subscriber.ConnectAsync();
        await publisher.ConnectAsync();
        var original = subscriber.GetPrimaryConnection();
        using var subscriptionCts = new CancellationTokenSource();
        await using var subscription = subscriber.SubscribeAsync<int>("renewal.events", subscriptionCts.Token).GetAsyncEnumerator();
        var next = subscription.MoveNextAsync().AsTask();
        try
        {
            await subscriber.InvokeAsync(string.Empty, BoltConnectionControl.GetExpiry, ReadOnlyMemory<byte>.Empty);
            await renewed.Task.WaitAsync(TimeSpan.FromSeconds(8));
            await subscriber.InvokeAsync(string.Empty, BoltConnectionControl.GetExpiry, ReadOnlyMemory<byte>.Empty);
            original.Transport.IsConnected.Should().BeTrue();

            await publisher.PublishAsync("renewal.events", 1);
            (await next.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();
            subscription.Current.Should().Be(1);
            next = subscription.MoveNextAsync().AsTask();
            await publisher.PublishAsync("renewal.events", 2);
            (await next.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();
            subscription.Current.Should().Be(2, "the old socket must not deliver a duplicate of event 1");
        }
        finally
        {
            subscriptionCts.Cancel();
            try { await next; } catch (OperationCanceledException) { }
        }
    }

    [Test]
    public async Task Renewal_PooledConnections_RotatesEachWithBoundedHeadroom()
    {
        var acquisitions = 0;
        await using var client = CreateClient("pooled", true, provider: _ =>
        {
            Interlocked.Increment(ref acquisitions);
            return ValueTask.FromResult<string?>(IssueTicket("pooled", 12));
        }, connections: 3, rpcTimeoutSeconds: 2);
        await client.ConnectAsync();

        var server = _app.Services.GetRequiredService<BoltServer>();
        var deadline = DateTime.UtcNow.AddSeconds(11);
        var peak = 0;
        while (DateTime.UtcNow < deadline)
        {
            var live = server.GetHealthSnapshot().LiveConnections;
            peak = Math.Max(peak, live);
            live.Should().BeLessThanOrEqualTo(6);
            await Task.Delay(20);
        }
        Volatile.Read(ref acquisitions).Should().BeGreaterThanOrEqualTo(6);
        peak.Should().BeGreaterThan(3, "replacement and draining sockets overlap within the quota");
        await WaitUntilAsync(() => _app.Services.GetRequiredService<BoltServer>()
            .GetHealthSnapshot().LiveConnections == 3, seconds: 5);

        client.GetHealthSnapshot().ConnectionCount.Should().Be(3);
        client.GetHealthSnapshot().TotalUnexpectedDisconnects.Should().Be(0);
        client.IsConnected.Should().BeTrue();
    }

    [TestCase(2, 12)]
    [TestCase(12, 8)]
    public async Task Renewal_IdleSocket_RetiresAfterBoundedGraceNotLocalIdle(int rpcTimeoutSeconds, int lifetimeSeconds)
    {
        var renewed = NewSignal();
        await using var client = CreateClient("grace", true,
            provider: _ => ValueTask.FromResult<string?>(IssueTicket("grace", lifetimeSeconds)),
            rpcTimeoutSeconds: rpcTimeoutSeconds);
        client.Reconnected += () => renewed.TrySetResult();
        await client.ConnectAsync();
        var original = client.GetPrimaryConnection();
        var response = await client.InvokeAsync(string.Empty, BoltConnectionControl.GetExpiry, ReadOnlyMemory<byte>.Empty);
        var expiry = DateTimeOffset.FromUnixTimeMilliseconds(BinaryPrimitives.ReadInt64LittleEndian(response.Data.Span));

        await renewed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var expectedGrace = Math.Min(rpcTimeoutSeconds, (expiry - DateTimeOffset.UtcNow).TotalSeconds - 0.25);
        var elapsed = Stopwatch.StartNew();
        original.Transport.IsConnected.Should().BeTrue("local idle cannot exclude work already routed by the Hub");
        await WaitUntilAsync(() => !original.Transport.IsConnected, seconds: lifetimeSeconds);

        elapsed.Elapsed.TotalSeconds.Should().BeGreaterThanOrEqualTo(expectedGrace - 0.4);
        elapsed.Elapsed.TotalSeconds.Should().BeLessThan(expectedGrace + 1.5);
        DateTimeOffset.UtcNow.Should().BeBefore(expiry.AddSeconds(1));
        client.IsConnected.Should().BeTrue();
        client.GetHealthSnapshot().TotalUnexpectedDisconnects.Should().Be(0);
    }

    [Test]
    public async Task Renewal_ProviderUnavailable_KeepsCurrentSocketButDoesNotExtendExpiry()
    {
        var acquisitions = 0;
        var failedRefresh = NewSignal();
        var disconnected = NewSignal();
        await using var client = CreateClient("failure", true, provider: _ =>
        {
            if (Interlocked.Increment(ref acquisitions) == 1)
                return ValueTask.FromResult<string?>(IssueTicket("failure", 4));
            failedRefresh.TrySetResult();
            throw new InvalidOperationException("issuer unavailable");
        });
        client.Disconnected += () => disconnected.TrySetResult();
        await client.ConnectAsync();
        var original = client.GetPrimaryConnection();

        await failedRefresh.Task.WaitAsync(TimeSpan.FromSeconds(4));
        client.GetPrimaryConnection().Should().BeSameAs(original);
        (await client.InvokeAsync(string.Empty, BoltConnectionControl.GetExpiry, ReadOnlyMemory<byte>.Empty))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(4));

        original.Transport.IsConnected.Should().BeFalse();
        client.IsConnected.Should().BeFalse("failed renewal must not keep an expired authenticated socket alive");
    }

    [Test]
    public async Task Renewal_CachedTicket_DoesNotReplaceUntilExpiryAdvances()
    {
        var initial = IssueTicket("cached", 8);
        var acquisitions = 0;
        var staleAttempt = NewSignal();
        var allowFreshTicket = false;
        var renewed = NewSignal();
        await using var client = CreateClient("cached", true, provider: _ =>
        {
            if (Interlocked.Increment(ref acquisitions) > 1)
                staleAttempt.TrySetResult();
            return ValueTask.FromResult<string?>(Volatile.Read(ref allowFreshTicket) ? IssueTicket("cached", 8) : initial);
        });
        client.Reconnected += () => renewed.TrySetResult();
        await client.ConnectAsync();
        var original = client.GetPrimaryConnection();
        await staleAttempt.Task.WaitAsync(TimeSpan.FromSeconds(8));

        client.GetPrimaryConnection().Should().BeSameAs(original);
        renewed.Task.IsCompleted.Should().BeFalse();
        Volatile.Write(ref allowFreshTicket, true);
        await renewed.Task.WaitAsync(TimeSpan.FromSeconds(3));

        client.GetPrimaryConnection().Should().NotBeSameAs(original);
    }

    [Test]
    public async Task Dispose_DuringRenewal_CancelsTokenAcquisitionAndDoesNotReviveClient()
    {
        var acquisitions = 0;
        var refreshStarted = NewSignal();
        var refreshCanceled = NewSignal();
        var client = CreateClient("dispose", true, provider: async ct =>
        {
            if (Interlocked.Increment(ref acquisitions) == 1)
                return IssueTicket("dispose", 4);
            refreshStarted.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) { refreshCanceled.TrySetResult(); throw; }
            return null;
        });
        await using (client)
        {
            await client.ConnectAsync();
            await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(4));
            await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            await refreshCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1));
            client.IsConnected.Should().BeFalse();
        }
    }

    [Test]
    public async Task Drain_InvalidPayload_DoesNotRemoveServiceFromRouting()
    {
        await using var caller = CreateClient("caller", false);
        await using var receiver = CreateClient("receiver", false);
        receiver.RegisterHandler("ping", (_, _) => Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty)));
        await caller.ConnectAsync();
        await receiver.ConnectAsync();

        (await receiver.InvokeAsync(string.Empty, BoltConnectionControl.Drain, new byte[] { 1 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await caller.InvokeAsync(ClientId("receiver"), "ping", ReadOnlyMemory<byte>.Empty))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestCase(BoltConnectionControl.GetExpiry)]
    [TestCase(BoltConnectionControl.Drain)]
    public async Task Control_UnregisteredAuthenticatedSocket_IsClosedWithoutResponse(string command)
    {
        using var socket = await OpenSocketAsync("unregistered", register: false);
        await SendFrameAsync(socket, writer => BoltCodec.WriteRequest(writer, Guid.NewGuid(), 0,
            BoltCodec.Fnv1aHash(ClientId("unregistered")), BoltCodec.Fnv1aHash(command), ReadOnlySpan<byte>.Empty));

        var (type, payload) = await ReceiveFrameAsync(socket);

        type.Should().Be(WebSocketMessageType.Close);
        payload.Should().BeEmpty("registration must precede either control command");
        await CloseSocketAsync(socket);
    }

    [TestCase(BoltConnectionControl.GetExpiry)]
    [TestCase(BoltConnectionControl.Drain)]
    public async Task Control_SpoofedSender_IsIgnoredWithoutDrainingSenderOrPeer(string command)
    {
        using var socket = await OpenSocketAsync("sender");
        await using var peer = CreateClient("peer", false);
        peer.RegisterHandler("ping", (_, _) => Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty)));
        await peer.ConnectAsync();
        var peerHash = BoltCodec.Fnv1aHash(ClientId("peer"));
        await SendFrameAsync(socket, writer => BoltCodec.WriteRequest(writer, Guid.NewGuid(), peerHash,
            peerHash, BoltCodec.Fnv1aHash(command), ReadOnlySpan<byte>.Empty));
        // A valid request on the same ordered socket is the processing barrier; the forged
        // request must produce no response, including no disclosure of the socket's expiry.
        var barrierId = Guid.NewGuid();
        await SendFrameAsync(socket, writer => BoltCodec.WriteRequest(writer, barrierId, 0,
            BoltCodec.Fnv1aHash(ClientId("sender")), BoltConnectionControl.GetExpiryHash, ReadOnlySpan<byte>.Empty));
        var (_, barrierBytes) = await ReceiveFrameAsync(socket);
        BoltCodec.TryReadResponse(barrierBytes, out var barrier, out _).Should().BeTrue();
        barrier.RequestId.Should().Be(barrierId);
        barrier.StatusCode.Should().Be(HttpStatusCode.OK);

        var ping = peer.InvokeAsync(ClientId("sender"), "ping", ReadOnlyMemory<byte>.Empty);
        var (_, requestBytes) = await ReceiveFrameAsync(socket);
        BoltCodec.TryReadRequest(requestBytes, out var request, out _).Should().BeTrue();
        await SendFrameAsync(socket, writer => BoltCodec.WriteResponse(writer, request.RequestId,
            HttpStatusCode.OK, ReadOnlySpan<byte>.Empty));
        (await ping.WaitAsync(TimeSpan.FromSeconds(2))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await peer.InvokeAsync(ClientId("peer"), "ping", ReadOnlyMemory<byte>.Empty))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await CloseSocketAsync(socket);
    }

    [Test]
    public async Task Drain_StillConsumesPrincipalQuota_UntilSocketCloses()
    {
        var sockets = new List<ClientWebSocket>();
        try
        {
            for (var i = 0; i < 6; i++)
                sockets.Add(await OpenSocketAsync("quota"));
            await SendFrameAsync(sockets[0], writer => BoltCodec.WriteRequest(writer, Guid.NewGuid(), 0,
                BoltCodec.Fnv1aHash(ClientId("quota")), BoltConnectionControl.DrainHash, ReadOnlySpan<byte>.Empty));
            var (_, bytes) = await ReceiveFrameAsync(sockets[0]);
            BoltCodec.TryReadResponse(bytes, out var drain, out _).Should().BeTrue();
            drain.StatusCode.Should().Be(HttpStatusCode.OK);

            using var rejected = await OpenSocketAsync("quota", register: false);
            await SendFrameAsync(rejected, writer => BoltCodec.WriteRegister(writer, ClientId("quota"), "quota"));
            var (_, ack) = await ReceiveFrameAsync(rejected);
            BoltCodec.TryReadRegisterAck(ack, out var accepted, out _).Should().BeTrue();
            accepted.Should().BeFalse("draining is not a way to bypass principal quotas");
            await CloseSocketAsync(rejected);
            await CloseSocketAsync(sockets[0]);
            await WaitUntilAsync(() => _app.Services.GetRequiredService<BoltServer>().GetHealthSnapshot().LiveConnections == 5);
            using var replacement = await OpenSocketAsync("quota");
            await CloseSocketAsync(replacement);
        }
        finally
        {
            foreach (var socket in sockets)
                socket.Dispose();
        }
    }

    [Test]
    public async Task Drain_OnlySendingConnection_LeavesPeerAndExistingTransportAlive()
    {
        await using var caller = CreateClient("caller", false);
        await using var previous = CreateClient("receiver", false);
        await using var replacement = CreateClient("receiver", false);
        previous.RegisterHandler("ping", (_, _) => Task.FromResult((HttpStatusCode.OK, (ReadOnlyMemory<byte>)new byte[] { 1 })));
        replacement.RegisterHandler("ping", (_, _) => Task.FromResult((HttpStatusCode.OK, (ReadOnlyMemory<byte>)new byte[] { 2 })));
        await caller.ConnectAsync();
        await previous.ConnectAsync();
        await replacement.ConnectAsync();

        (await previous.InvokeAsync(ClientId("receiver"), BoltConnectionControl.Drain, ReadOnlyMemory<byte>.Empty))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        for (var i = 0; i < 4; i++)
        {
            var result = await caller.InvokeAsync(ClientId("receiver"), "ping", ReadOnlyMemory<byte>.Empty);
            result.StatusCode.Should().Be(HttpStatusCode.OK);
            result.Data.ToArray().Should().Equal(new byte[] { 2 });
        }
        previous.IsConnected.Should().BeTrue("draining must not abort accepted work");
    }

    [Test]
    public async Task Renewal_ReplacementIdentityMismatch_DoesNotReplaceAuthenticatedConnection()
    {
        var acquisitions = 0;
        var attempted = NewSignal();
        await using var client = CreateClient("original", true, provider: _ =>
        {
            var attempt = Interlocked.Increment(ref acquisitions);
            if (attempt == 1)
                return ValueTask.FromResult<string?>(IssueTicket("original", 8));
            // The next attempt proves the preceding mismatched registration finished and
            // was rejected, rather than asserting while its handshake is still in flight.
            if (attempt >= 3)
                attempted.TrySetResult();
            return ValueTask.FromResult<string?>(IssueTicket("different-service", 60));
        });
        await client.ConnectAsync();
        var original = client.GetPrimaryConnection();
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(8));
        (await client.InvokeAsync(string.Empty, BoltConnectionControl.GetExpiry, ReadOnlyMemory<byte>.Empty))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        client.GetPrimaryConnection().Should().BeSameAs(original);
    }

    private BoltClient CreateClient(
        string name, bool renew, Func<CancellationToken, ValueTask<string?>>? provider = null, int connections = 1,
        int rpcTimeoutSeconds = 12) => new(
        _uri, ClientId(name), name,
        new BoltClientOptions
        {
            AccessToken = renew ? null : IssueTicket(name, 60),
            AccessTokenProvider = renew ? provider ?? (_ => ValueTask.FromResult<string?>(IssueTicket(name, 8))) : null,
            AutoReconnect = renew,
            RenewConnectionsBeforeExpiry = renew,
            ConnectionRenewalLeadSeconds = 4,
            MinConnections = connections,
            MaxConnections = connections,
            RpcTimeoutSeconds = rpcTimeoutSeconds,
            LargePayloadThreshold = 1024,
            StreamChunkSize = 512,
            TransportAttemptTimeoutMs = 1000
        }, NullLogger<BoltClient>.Instance);

    private async Task<ClientWebSocket> OpenSocketAsync(string name, bool register = true)
    {
        var socket = new ClientWebSocket();
        try
        {
            socket.Options.SetRequestHeader("Authorization", "Bearer " + IssueTicket(name, 60));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await socket.ConnectAsync(_uri, timeout.Token);
            if (register)
            {
                await SendFrameAsync(socket, writer => BoltCodec.WriteRegister(writer, ClientId(name), name));
                var (_, bytes) = await ReceiveFrameAsync(socket);
                BoltCodec.TryReadRegisterAck(bytes, out var accepted, out _).Should().BeTrue();
                accepted.Should().BeTrue();
            }
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async Task SendFrameAsync(ClientWebSocket socket, Action<IBufferWriter<byte>> write)
    {
        var writer = new ArrayBufferWriter<byte>();
        write(writer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await socket.SendAsync(writer.WrittenMemory, WebSocketMessageType.Binary, true, timeout.Token);
    }

    private static async Task<(WebSocketMessageType Type, byte[] Payload)> ReceiveFrameAsync(ClientWebSocket socket)
    {
        var buffer = new byte[4096];
        var count = 0;
        ValueWebSocketReceiveResult result;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        do
        {
            result = await socket.ReceiveAsync(buffer.AsMemory(count), timeout.Token);
            count += result.Count;
        } while (!result.EndOfMessage);
        return (result.MessageType, buffer[..count]);
    }

    private static async Task CloseSocketAsync(ClientWebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", timeout.Token);
    }

    private string IssueTicket(string name, int lifetimeSeconds)
    {
        var token = Guid.NewGuid().ToString("N");
        _tickets[token] = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", name), new Claim("client_id", name), new Claim("scope", "bolt.service"),
            new Claim("exp", DateTimeOffset.UtcNow.AddSeconds(lifetimeSeconds).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        ], "test"));
        return token;
    }

    private static string ClientId(string name) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(Func<bool> condition, int seconds = 3)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        condition().Should().BeTrue();
    }
}
