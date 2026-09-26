using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using System.IdentityModel.Tokens.Jwt;
using Bolt.Protocol.Transport;
using Bolt.Server;
using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Integration.Clients;
using Communications.Integration.Drivers;
using XFramework.Integration.Security;
using Yap.Contracts;

namespace Yap.Services;

/// <summary>Yap's cookie-authenticated browser transport. The dedicated Bolt server exposes
/// only the chat command; module authorization stays in the existing actor-bound services.</summary>
public sealed class YapChatGateway : IDisposable
{
    private const string ClientClaim = "yap_chat_client_id";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly object ticketGate = new();
    private readonly Dictionary<string, Admission> tickets = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Connection> connections = new();
    private readonly IServiceScopeFactory scopes;
    private readonly YapSessions sessions;
    private readonly YapCallGateway calls;
    private readonly IConfiguration configuration;
    private readonly ILogger<YapChatGateway> logger;
    private readonly BoltServer server;

    public YapChatGateway(IServiceScopeFactory scopes, YapSessions sessions, YapCallGateway calls,
        IConfiguration configuration, ILogger<YapChatGateway> logger, ILogger<BoltServer> boltLogger)
    {
        this.scopes = scopes;
        this.sessions = sessions;
        this.calls = calls;
        this.configuration = configuration;
        this.logger = logger;
        server = new BoltServer(boltLogger, new BoltServerOptions
        {
            RequireSecureTransport = true, LocalHandlersOnly = true, RegistrationClientIdClaim = ClientClaim,
            MaxFrameBytes = 4 * 1024 * 1024, SendQueueCapacity = 64, SendQueueByteCapacity = 8 * 1024 * 1024,
            SendEnqueueTimeoutMs = 1000, MaxConnectionsPerPrincipal = 4, MaxConnectionLifetimeSeconds = 300,
            RpcRequestsPerSecond = 40, RpcRequestBurst = 80,
            RpcInboundBytesPerSecond = 1024 * 1024, RpcInboundByteBurst = 4 * 1024 * 1024
        });
        server.RegisterHandler("yap.chat", CommandAsync);
        server.ClientRegistered += RegisteredAsync;
    }

    public async Task<ChatSocketTicket> CreateTicketAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await sessions.ContainsAsync(user, ct)) throw new YapApiException(401, "Sign in again.");
        var binding = Binding(user);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var clientId = $"yap-chat-{Guid.NewGuid():N}";
        lock (ticketGate)
        {
            foreach (var key in tickets.Where(x => x.Value.ExpiresAt <= DateTimeOffset.UtcNow || x.Value.Binding == binding).Select(x => x.Key).ToArray())
                tickets.Remove(key);
            if (tickets.Count >= 1024) throw new YapApiException(429, "Try connecting again shortly.");
            tickets[token] = new(binding, clientId, DateTimeOffset.UtcNow.AddSeconds(30));
        }
        var tenant = Guid.Parse(user.FindFirstValue(YapAuth.TenantClaim)!);
        var credential = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return new($"/api/chat/socket?ticket={token}&account={tenant:N}:{credential:N}", clientId);
    }

    public async Task AcceptSocketAsync(HttpContext context)
    {
        if (!context.Request.IsHttps) throw new YapApiException(426, "A secure connection is required.");
        if (!YapCallGateway.HasSameOrigin(context.Request)) throw new YapApiException(403, "Open this connection from Yap.");
        if (!context.WebSockets.IsWebSocketRequest) throw new YapApiException(400, "A WebSocket connection is required.");
        if (!await sessions.ContainsAsync(context.User, context.RequestAborted)) throw new YapApiException(401, "Sign in again.");
        Admission ticket;
        lock (ticketGate)
        {
            var token = context.Request.Query["ticket"].ToString();
            if (!tickets.TryGetValue(token, out ticket!) || ticket.ExpiresAt <= DateTimeOffset.UtcNow || ticket.Binding != Binding(context.User))
                throw new YapApiException(403, "This connection has expired.");
            tickets.Remove(token);
        }
        var identity = new ClaimsIdentity(context.User.Identity as ClaimsIdentity);
        identity.AddClaim(new(ClientClaim, ticket.ClientId));
        var user = new ClaimsPrincipal(identity);
        using var connection = new Connection(user, context.RequestAborted);
        lock (ticketGate)
        {
            if (connections.Values.Count(x => Binding(x.User) == ticket.Binding) >= 4)
                throw new YapApiException(429, "Too many active app connections.");
            if (!connections.TryAdd(ticket.ClientId, connection)) throw new YapApiException(409, "This connection is already open.");
        }
        try
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await using var transport = new WebSocketBoltConnection(socket);
            var receive = server.HandleConnectionAsync(transport, user, connection.Token, isSecureTransport: true);
            var pump = PumpAsync(connection);
            var validation = ValidateLifetimeAsync(connection);
            await Task.WhenAny(receive, pump, validation);
            await connection.Lifetime.CancelAsync();
            try { await Task.WhenAll(receive, pump, validation); }
            catch (OperationCanceledException) when (connection.Token.IsCancellationRequested) { }
        }
        catch (Exception error) when (context.Response.HasStarted)
        {
            // An upgraded response can only end its socket; admission errors before the
            // upgrade still reach the API filter and retain their HTTP status.
            // No request URL, cookie, session identity, payload or ticket is logged.
            logger.LogInformation("Chat connection closed. FailureType={FailureType}", error.GetType().Name);
        }
        finally
        {
            connections.TryRemove(ticket.ClientId, out _);
            await connection.Lifetime.CancelAsync();
        }
    }

    private Task RegisteredAsync(BoltClientConnectionEvent value, CancellationToken ct)
    {
        if (connections.TryGetValue(value.ClientId, out var connection)) connection.Registered.TrySetResult(value.ConnectionId);
        return Task.CompletedTask;
    }

    private async Task PumpAsync(Connection connection)
    {
        var connectionId = await connection.Registered.Task.WaitAsync(TimeSpan.FromSeconds(10), connection.Token);
        await using var scope = scopes.CreateAsyncScope();
        var (session, directory) = await ServicesAsync(scope.ServiceProvider, connection.User, connection.Token);
        var actor = await sessions.GetActorAsync(connection.User, connection.Token);
        connection.Lifetime.CancelAfter(SubscriptionLifetime(actor.AccessToken, DateTimeOffset.UtcNow));
        connection.SessionReady.TrySetResult(session);

        // Reconnects deliberately reconcile durable history. Live subscriptions are independent
        // per tab; the durable backend outbox/history, not a host-memory cursor, repairs gaps.
        connection.Enqueue(new ChatSocketEvent(Guid.NewGuid(), 0, "refresh"));
        using var callEvents = calls.Subscribe(connection.User, value => connection.Enqueue(new ChatSocketEvent(
            Guid.NewGuid(), 0, "call", Body: JsonSerializer.SerializeToElement(value, Json))));
        await session.SubscribeLiveUserEventsAsync(update =>
        {
            connection.Enqueue(update);
            return Task.CompletedTask;
        }, connection.Token);

        await foreach (var queued in connection.Pending.Reader.ReadAllAsync(connection.Token))
        {
            Interlocked.Add(ref connection.PendingBytes, -queued.Bytes);
            var item = queued.Value;
            if (!await sessions.ContainsAsync(connection.User, connection.Token)) throw new YapApiException(401, "Sign in again.");
            IReadOnlyList<ChatSocketEvent> values = item is ChatSocketEvent direct ? [direct] : [];
            if (item is CommunicationsRealtimeEvent update)
            {
                if (update.TenantId != session.TenantId) continue;
                // Topic permission alone is insufficient: resolve every message using current
                // membership, visibility and encryption audience checks, without marking delivery.
                var hint = YapRealtime.Hint(update);
                if (hint is null) values = [new(update.EventId, 0, "refresh")];
                else
                {
                    // The active device already applied its own receipts optimistically. Avoid
                    // querying/projecting them back through the critical incoming-message queue.
                    if (hint.Kind is "MessagesRead" or "MessagesDelivered" && hint.ActorId == session.CredentialId &&
                        Volatile.Read(ref connection.WatchedThread) == hint.ThreadId.ToString("N")) continue;
                    var batch = TakeMessageBatch(update, hint, connection.Pending.Reader, session.TenantId,
                        session.CredentialId, Volatile.Read(ref connection.WatchedThread), out var drainedBytes);
                    Interlocked.Add(ref connection.PendingBytes, -drainedBytes);
                    values = await ProjectMessageBatchAsync(batch, session, directory, connection.Token);
                }
            }
            foreach (var value in values)
            {
                if (value.Kind == "typing" && value.ThreadId is { } typingThread)
                {
                    var current = await session.GetThreadAsync(typingThread, connection.Token);
                    if (!current.IsSuccess || current.Response is null ||
                        !current.Response.Members.Any(x => x.CredentialId == session.CredentialId) ||
                        ((int)current.Response.Features & (int)ChatFeature.Typing) == 0) continue;
                }
                if (!await sessions.ContainsAsync(connection.User, connection.Token)) throw new YapApiException(401, "Sign in again.");
                var sequence = Interlocked.Increment(ref connection.Sequence);
                if (sequence - Interlocked.Read(ref connection.Acknowledged) > 128)
                    throw new YapApiException(409, "Reconnect to synchronize messages.");
                var payload = JsonSerializer.SerializeToUtf8Bytes(value with { Sequence = sequence }, Json);
                await server.SendPushAsync(connectionId, "yap.chat.event", payload, connection.Token);
            }
        }
    }

    internal static List<(CommunicationsRealtimeEvent Event, ChatUpdateHint Hint)> TakeMessageBatch(
        CommunicationsRealtimeEvent first, ChatUpdateHint hint, ChannelReader<PendingEvent> reader,
        Guid tenant, Guid credential, string? watchedThread, out long drainedBytes)
    {
        List<(CommunicationsRealtimeEvent Event, ChatUpdateHint Hint)> batch = [(first, hint)];
        var ids = hint.MessageIds.ToHashSet();
        drainedBytes = 0;
        // Drain only work already waiting. Different threads, typing, calls and refreshes
        // remain ordering barriers; no batching timer delays an otherwise idle connection.
        for (var count = 1; count < 32 && reader.TryPeek(out var queued); count++)
        {
            if (queued.Value is not CommunicationsRealtimeEvent next || next.TenantId != tenant ||
                YapRealtime.Hint(next) is not { } nextHint || nextHint.ThreadId != hint.ThreadId ||
                ids.Union(nextHint.MessageIds).Count() > 50) break;
            if (!reader.TryRead(out _)) break;
            drainedBytes += queued.Bytes;
            if (nextHint.Kind is "MessagesRead" or "MessagesDelivered" && nextHint.ActorId == credential &&
                watchedThread == nextHint.ThreadId.ToString("N")) continue;
            ids.UnionWith(nextHint.MessageIds);
            batch.Add((next, nextHint));
        }
        return batch;
    }

    internal static async Task<IReadOnlyList<ChatSocketEvent>> ProjectMessageBatchAsync(
        IReadOnlyList<(CommunicationsRealtimeEvent Event, ChatUpdateHint Hint)> batch,
        ICommunicationsChatSession session, IChatDirectory directory, CancellationToken ct)
    {
        var thread = batch[0].Hint.ThreadId;
        var ids = batch.SelectMany(x => x.Hint.MessageIds).Distinct().ToList();
        try
        {
            var data = YapApi.Require(await session.GetMessageProjectionsAsync(thread, ids, ct));
            var mapped = await YapApi.MapMessagesAsync(thread, data, session, directory, ct);
            var messages = mapped.Items.ToDictionary(x => x.Id);
            return batch.Select(item =>
            {
                var (update, hint) = item;
                if (hint.MessageIds.Any(id => !messages.TryGetValue(id, out var message) || message.IsThreadReply))
                    return new ChatSocketEvent(update.EventId, 0, "refresh");
                var privateReceipt = hint.Kind is "MessagesRead" or "MessagesDelivered" && hint.ActorId != session.CredentialId;
                return new ChatSocketEvent(update.EventId, 0, privateReceipt ? "MessageUpdated" : hint.Kind, thread,
                    privateReceipt ? null : hint.ActorId, hint.MessageIds, hint.MessageIds.Select(id => messages[id]).ToList());
            }).ToList();
        }
        catch (YapApiException error) when (error.Status is 403 or 404)
        { return batch.Select(x => new ChatSocketEvent(x.Event.EventId, 0, "refresh")).ToList(); }
    }

    private async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> CommandAsync(BoltRequestContext context,
        ReadOnlyMemory<byte> payload, Guid requestId, CancellationToken ct)
    {
        Connection? connection = null;
        try
        {
            if (context.ClientId is null || !connections.TryGetValue(context.ClientId, out connection) ||
                context.User is null || Binding(context.User) != Binding(connection.User) ||
                !connection.Registered.Task.IsCompletedSuccessfully || connection.Registered.Task.Result != context.ConnectionId)
                return Response(401, new { title = "Sign in again." });
            if (!await sessions.ContainsAsync(connection.User, ct)) return SessionEnded();
            var request = ParseRequest(payload);
            if (request.Operation == "ack")
            {
                if (!request.Body.TryGetProperty("sequence", out var acknowledgment) ||
                    acknowledgment.ValueKind != JsonValueKind.Number || !acknowledgment.TryGetInt64(out var sequence))
                    throw new YapApiException(400, "Invalid event acknowledgment.");
                if (sequence < 0 || sequence > Interlocked.Read(ref connection.Sequence)) throw new YapApiException(400, "Invalid event acknowledgment.");
                long previous;
                do { previous = Interlocked.Read(ref connection.Acknowledged); if (sequence <= previous) break; }
                while (Interlocked.CompareExchange(ref connection.Acknowledged, sequence, previous) != previous);
                return Response(204);
            }
            await using var scope = scopes.CreateAsyncScope();
            var (session, _) = await ServicesAsync(scope.ServiceProvider, connection.User, ct);
            switch (request.Operation)
            {
                case "send":
                    return Response(200, await YapChatCommands.SendAsync(Body<SendMessage>(request), session,
                        configuration.GetValue("Yap:Encryption:Enabled", true), ct));
                case "read":
                    await YapChatCommands.ReadAsync(Body<ReadMessages>(request), session, ct);
                    break;
                case "delivered":
                    var receipt = Body<ReadMessages>(request);
                    if (receipt.MessageIds is null || receipt.MessageIds.Count is 0 or > 50) throw new YapApiException(400, "Choose up to 50 messages.");
                    YapApi.Require(await session.MarkDeliveredAsync(receipt.ThreadId, receipt.MessageIds, ct));
                    break;
                case "typing":
                    await YapChatCommands.TypingAsync(Body<ThreadAction>(request), session, ct);
                    break;
                case "watch":
                    if (!request.Body.TryGetProperty("threadId", out var thread) ||
                        (thread.ValueKind != JsonValueKind.Null &&
                         (thread.ValueKind != JsonValueKind.String || !thread.TryGetGuid(out _))))
                        throw new YapApiException(400, "Choose a conversation.");
                    await WatchAsync(connection, await connection.SessionReady.Task.WaitAsync(ct),
                        thread.ValueKind == JsonValueKind.Null ? null : thread.GetGuid(), ct);
                    break;
                default: throw new YapApiException(400, "Choose a supported chat operation.");
            }
            return Response(204);
        }
        catch (YapApiException error) when (error.Status == 401) { return await RejectedAsync(connection, ct); }
        catch (YapApiException error) { return Response(error.Status, new { title = error.Message }); }
        catch (UnauthorizedAccessException) { return await RejectedAsync(connection, ct); }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            // Backend transport failures must keep the durable outbox retryable. Never
            // classify an execution exception as malformed input or log its payload.
            logger.LogInformation("Chat command temporarily unavailable. FailureType={FailureType}", error.GetType().Name);
            return Retryable();
        }
    }

    // The socket form of YapApiFilter.RejectedAsync: a downstream 401 is settled by one
    // refresh, and only a refused one tells the browser its sign-in ended.
    private async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> RejectedAsync(Connection? connection, CancellationToken ct)
    {
        try
        {
            if (await sessions.ConfirmRejectionAsync(connection?.User, ct)) return SessionEnded();
        }
        // Unconfirmed is not ended: the refresh could not be reached, so the sign-in stays.
        catch (Exception error) when (!ct.IsCancellationRequested)
        { logger.LogInformation("Chat could not confirm a refused sign-in. FailureType={FailureType}", error.GetType().Name); }
        return Retryable();
    }

    private static (HttpStatusCode, ReadOnlyMemory<byte>) SessionEnded() =>
        Response(401, new { title = "Your session ended. Sign in again." }, sessionEnded: true);
    private static (HttpStatusCode, ReadOnlyMemory<byte>) Retryable() =>
        Response(503, new { title = "Messages will retry when the connection is restored." });

    private async Task WatchAsync(Connection connection, ICommunicationsChatSession session, Guid? thread, CancellationToken ct)
    {
        await connection.WatchGate.WaitAsync(ct);
        try
        {
            if (thread.HasValue) YapApi.Require(await session.GetThreadAsync(thread.Value, ct));
            Volatile.Write(ref connection.WatchedThread, thread?.ToString("N"));
            if (connection.TypingLifetime is { } previous) { await previous.CancelAsync(); previous.Dispose(); }
            connection.TypingLifetime = null;
            if (thread is null) return;
            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(connection.Token);
            connection.TypingLifetime = lifetime;
            await session.SubscribeTypingAsync(thread.Value, value =>
            {
                if (!lifetime.IsCancellationRequested && value.TenantId == session.TenantId && value.ThreadId == thread.Value &&
                    value.CredentialId != session.CredentialId)
                    connection.Enqueue(new ChatSocketEvent(Guid.NewGuid(), 0, "typing", value.ThreadId,
                    Body: JsonSerializer.SerializeToElement(YapChatCommands.Typing(value), Json)));
                return Task.CompletedTask;
            }, lifetime.Token);
        }
        finally { connection.WatchGate.Release(); }
    }

    private async Task<(ICommunicationsChatSession, IChatDirectory)> ServicesAsync(IServiceProvider services, ClaimsPrincipal user, CancellationToken ct)
    {
        var actor = new FixedActor(sessions, user);
        var tokenScope = services.GetRequiredService<IActorAccessTokenScope>();
        var client = new CommunicationsChatClient(services.GetRequiredService<ICommunicationsServiceWrapper>(), configuration, actor, tokenScope);
        return (await client.ForCurrentActorAsync(ct: ct), new ChatDirectory(services, actor, tokenScope));
    }

    private async Task ValidateLifetimeAsync(Connection connection)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(connection.Token))
            if (!await sessions.ContainsAsync(connection.User, connection.Token)) return;
    }

    private static ChatSocketRequest ParseRequest(ReadOnlyMemory<byte> payload)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ChatSocketRequest>(payload.Span, Json);
            if (request is null || request.Body.ValueKind != JsonValueKind.Object)
                throw new YapApiException(400, "The chat request is invalid.");
            return request;
        }
        catch (JsonException) { throw new YapApiException(400, "The chat request is invalid."); }
    }

    private static T Body<T>(ChatSocketRequest request)
    {
        try { return request.Body.Deserialize<T>(Json) ?? throw new YapApiException(400, "The chat request is invalid."); }
        catch (JsonException) { throw new YapApiException(400, "The chat request is invalid."); }
    }
    internal static TimeSpan SubscriptionLifetime(string? accessToken, DateTimeOffset now)
    {
        var maximum = TimeSpan.FromMinutes(5);
        // The token came from YapSessions, not the browser. Reading exp only shortens the
        // connection lifetime; it grants no trust and never replaces backend token validation.
        var reader = new JwtSecurityTokenHandler();
        if (accessToken is null || !reader.CanReadToken(accessToken)) return maximum;
        var token = reader.ReadJwtToken(accessToken);
        if (token.Payload.Expiration is not { } expiry) return maximum;
        var remaining = DateTimeOffset.FromUnixTimeSeconds(expiry) - now - TimeSpan.FromSeconds(15);
        return remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining < maximum ? remaining : maximum;
    }
    private static (HttpStatusCode, ReadOnlyMemory<byte>) Response(int status, object? body = null, bool sessionEnded = false) =>
        (HttpStatusCode.OK, JsonSerializer.SerializeToUtf8Bytes(new ChatSocketResponse(status,
            body is null ? null : JsonSerializer.SerializeToElement(body, Json), sessionEnded), Json));
    private static string Binding(ClaimsPrincipal user) => string.Join(':', user.FindFirstValue(YapAuth.TenantClaim),
        user.FindFirstValue(ClaimTypes.NameIdentifier), user.FindFirstValue(YapAuth.SessionClaim));
    public void Dispose()
    {
        foreach (var connection in connections.Values) connection.Lifetime.Cancel();
        server.ClientRegistered -= RegisteredAsync;
        server.Dispose();
    }

    private sealed record Admission(string Binding, string ClientId, DateTimeOffset ExpiresAt);
    private sealed class FixedActor(YapSessions sessions, ClaimsPrincipal user) : ICommunicationsChatActorProvider
    { public async ValueTask<CommunicationsChatActor?> GetCurrentActorAsync(CancellationToken ct = default) => await sessions.GetActorAsync(user, ct); }

    internal sealed record PendingEvent(object Value, int Bytes);
    private sealed class Connection : IDisposable
    {
        public Connection(ClaimsPrincipal user, CancellationToken aborted)
        {
            User = user;
            Lifetime = CancellationTokenSource.CreateLinkedTokenSource(aborted);
            // Rebind the upstream actor-authorized subscription frequently, including its
            // refreshed access token; cookie expiry is also checked independently below.
            Lifetime.CancelAfter(TimeSpan.FromMinutes(5));
            Token = Lifetime.Token;
        }
        public ClaimsPrincipal User { get; }
        public CancellationTokenSource Lifetime { get; }
        public CancellationToken Token { get; }
        public TaskCompletionSource<string> Registered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ICommunicationsChatSession> SessionReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Channel<PendingEvent> Pending { get; } = Channel.CreateBounded<PendingEvent>(new BoundedChannelOptions(128)
            { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
        public SemaphoreSlim WatchGate { get; } = new(1, 1);
        public CancellationTokenSource? TypingLifetime { get; set; }
        public long Sequence;
        public long Acknowledged;
        public long PendingBytes;
        public string? WatchedThread;
        public void Enqueue(object item)
        {
            // Durable events are never silently dropped. Closing forces the browser to
            // reconcile durable history before consuming live pushes on a new socket.
            if (Token.IsCancellationRequested) return;
            var bytes = item is CommunicationsRealtimeEvent update
                ? System.Text.Encoding.UTF8.GetByteCount(update.PayloadJson) + 256
                : JsonSerializer.SerializeToUtf8Bytes(item, Json).Length;
            if (Interlocked.Add(ref PendingBytes, bytes) > 8 * 1024 * 1024 || !Pending.Writer.TryWrite(new(item, bytes)))
            {
                Interlocked.Add(ref PendingBytes, -bytes);
                Lifetime.Cancel();
            }
        }
        public void Dispose()
        {
            TypingLifetime?.Cancel();
            TypingLifetime?.Dispose();
            Lifetime.Dispose();
            // SemaphoreSlim has no wait handle here; an already-cancelled in-flight watch may
            // still release it while the transport finishes its local invocation cleanup.
        }
    }
}
