using System.Security.Claims;
using System.Security.Cryptography;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using Communications.Integration.Clients;
using Communications.Integration.Drivers;
using XFramework.Integration.Security;
using Yap.Contracts;

namespace Yap.Services;

/// <summary>One Yap instance, trusted-server TLS relay. This is explicitly not end-to-end encryption.</summary>
public sealed class YapCallGateway : IBoltCallAuthorizer, IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, ActiveInvite> invites = [];
    private readonly Dictionary<string, Ticket> tickets = new(StringComparer.Ordinal);
    private readonly Dictionary<(Guid Tenant, Guid User), List<Action<YapCallEvent>>> listeners = [];
    private readonly IServiceScopeFactory scopes;
    private readonly Timer cleanup;
    public bool Enabled { get; }
    public BoltServer Server { get; }

    public YapCallGateway(IConfiguration configuration, IServiceScopeFactory scopes, ILogger<BoltServer> logger)
    {
        this.scopes = scopes;
        Enabled = configuration.GetValue<bool>("Yap:Calls:Enabled");
        if (Enabled && configuration["Yap:Calls:SecurityMode"] != "TrustedServerTls")
            throw new InvalidOperationException("Yap calls require the explicit TrustedServerTls security mode.");
        Server = new BoltServer(logger, new BoltServerOptions
        {
            MediaEnabled = Enabled, RequireSecureTransport = true, AuthenticatedMediaOnly = true,
            CallAuthorizer = this, MaxActiveCalls = 64, MaxActiveCallsPerPrincipal = 1,
            MaxCallParticipants = 2, MaxMediaStreamsPerPrincipal = 2,
            MaxFrameBytes = 64 * 1024, SendQueueCapacity = 64, SendQueueByteCapacity = 1024 * 1024,
            MaxConnectionsPerPrincipal = 2, MaxConnectionLifetimeSeconds = 3600
        });
        cleanup = new Timer(_ => Prune(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public static string ClientId(Guid call, Guid user) => $"yap-media-{call:N}-{user:N}";
    private static (Guid Tenant, Guid User) Identity(ClaimsPrincipal user) => (
        Guid.Parse(user.FindFirstValue(YapAuth.TenantClaim)!), Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!));

    public async Task<YapCallInvite> StartAsync(ClaimsPrincipal user, StartYapCall request, CancellationToken ct)
    {
        RequireEnabled();
        var (tenant, caller) = Identity(user);
        if (request.ThreadId == Guid.Empty || request.RecipientId == Guid.Empty || caller == request.RecipientId)
            throw new YapApiException(400, "Choose someone in this conversation to call.");
        await VerifyMembershipAsync(user, request.ThreadId, request.RecipientId, ct);
        var invite = new YapCallInvite(Guid.NewGuid(), request.ThreadId, caller,
            user.Identity?.Name ?? "Someone", request.RecipientId, DateTimeOffset.UtcNow.AddSeconds(60));
        lock (gate)
        {
            if (invites.Count >= 64 || invites.Values.Any(x => x.Tenant == tenant &&
                (x.Invite.CallerId == caller || x.Invite.RecipientId == caller ||
                 x.Invite.CallerId == request.RecipientId || x.Invite.RecipientId == request.RecipientId)))
                throw new YapApiException(409, "One of you is already in a call.");
            invites.Add(invite.Id, new ActiveInvite(tenant, invite));
        }
        Publish(tenant, invite.RecipientId, new("incoming", invite));
        return invite;
    }

    public async Task<YapCallConnection> ConnectAsync(ClaimsPrincipal user, Guid callId, CancellationToken ct)
    {
        RequireEnabled();
        var (tenant, credential) = Identity(user);
        ActiveInvite active;
        lock (gate) active = GetInvite(tenant, credential, callId);
        var recipient = active.Invite.CallerId == credential ? active.Invite.RecipientId : active.Invite.CallerId;
        await VerifyMembershipAsync(user, active.Invite.ThreadId, recipient, ct);
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        lock (gate)
        {
            active = GetInvite(tenant, credential, callId);
            if (active.Connected.Contains(credential)) throw new YapApiException(409, "This call is already connected on this account.");
            foreach (var old in tickets.Where(x => x.Value.CallId == callId && x.Value.User == credential).Select(x => x.Key).ToArray()) tickets.Remove(old);
            tickets[ticket] = new Ticket(callId, tenant, credential, user.FindFirstValue(YapAuth.SessionClaim)!, DateTimeOffset.UtcNow.AddSeconds(30));
        }
        return new(callId, $"/api/chat/calls/socket?ticket={ticket}", ClientId(callId, credential), ClientId(callId, recipient));
    }

    public void End(ClaimsPrincipal user, Guid callId)
    {
        var (tenant, credential) = Identity(user);
        ActiveInvite active;
        lock (gate) { active = GetInvite(tenant, credential, callId); RemoveLocked(callId); }
        PublishBoth(active, "ended");
        active.Lifetime.Cancel();
    }

    public void Ready(ClaimsPrincipal user, Guid callId)
    {
        var (tenant, credential) = Identity(user);
        ActiveInvite active;
        lock (gate)
        {
            active = GetInvite(tenant, credential, callId);
            if (!active.Registered.Contains(credential)) throw new YapApiException(409, "Connect to this call first.");
            if (!active.Ready.Add(credential)) return;
        }
        PublishBoth(active, "ready", credential);
    }

    public IDisposable Subscribe(ClaimsPrincipal user, Action<YapCallEvent> handler)
    {
        var key = Identity(user);
        YapCallEvent[] pending;
        lock (gate)
        {
            if (!listeners.TryGetValue(key, out var handlers)) listeners[key] = handlers = [];
            if (handlers.Count >= 4) throw new YapApiException(429, "Too many active app connections.");
            handlers.Add(handler);
            pending = invites.Values.Where(x => x.Tenant == key.Tenant &&
                    (x.Invite.CallerId == key.User || x.Invite.RecipientId == key.User) &&
                    (x.Answered || x.Invite.ExpiresAt > DateTimeOffset.UtcNow))
                .SelectMany(x =>
                {
                    var events = new List<YapCallEvent>();
                    if (!x.Answered && x.Invite.RecipientId == key.User)
                        events.Add(new("incoming", x.Invite));
                    events.AddRange(x.Ready.Where(x.Connected.Contains).Select(credential => new YapCallEvent("ready", x.Invite, credential)));
                    return events;
                }).ToArray();
            // Snapshot precedes live events: an End racing subscription must not be followed
            // by a stale incoming snapshot that reopens a finished call in the client.
            foreach (var value in pending) handler(value);
        }
        return new Subscription(() => { lock (gate) { if (!listeners.TryGetValue(key, out var handlers)) return; handlers.Remove(handler); if (handlers.Count == 0) listeners.Remove(key); } });
    }

    public async Task AcceptSocketAsync(HttpContext context)
    {
        RequireEnabled();
        if (!context.Request.IsHttps) throw new YapApiException(426, "A secure connection is required for calls.");
        if (!HasSameOrigin(context.Request)) throw new YapApiException(403, "Open this call from Yap.");
        if (!context.WebSockets.IsWebSocketRequest) throw new YapApiException(400, "A call connection is required.");
        var (tenant, credential) = Identity(context.User);
        var token = context.Request.Query["ticket"].ToString();
        Ticket ticket;
        ActiveInvite active;
        lock (gate)
        {
            if (!tickets.TryGetValue(token, out ticket!) || ticket.ExpiresAt <= DateTimeOffset.UtcNow ||
                ticket.Tenant != tenant || ticket.User != credential || ticket.Session != context.User.FindFirstValue(YapAuth.SessionClaim))
                throw new YapApiException(403, "This call connection has expired.");
            active = GetInvite(tenant, credential, ticket.CallId);
            tickets.Remove(token);
            if (!active.Connected.Add(credential)) throw new YapApiException(409, "This call is already connected.");
        }
        try
        {
            var other = active.Invite.CallerId == credential ? active.Invite.RecipientId : active.Invite.CallerId;
            await VerifyMembershipAsync(context.User, active.Invite.ThreadId, other, context.RequestAborted);
            var identity = new ClaimsIdentity(context.User.Identity as ClaimsIdentity);
            identity.AddClaim(new("bolt_media_client_id", ClientId(ticket.CallId, credential)));
            identity.AddClaim(new("yap_call_id", ticket.CallId.ToString()));
            var principal = new ClaimsPrincipal(identity);
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, active.Lifetime.Token);
            lifetime.CancelAfter(TimeSpan.FromHours(1));
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await using var transport = new ReadyTransport(new WebSocketBoltConnection(socket), () =>
            { lock (gate) active.Registered.Add(credential); });
            await Server.HandleConnectionAsync(transport, principal, lifetime.Token, isSecureTransport: context.Request.IsHttps);
        }
        finally
        {
            lock (gate) { active.Connected.Remove(credential); RemoveLocked(ticket.CallId); }
            PublishBoth(active, "ended");
            active.Lifetime.Cancel();
        }
    }

    public async ValueTask<bool> AuthorizeAsync(BoltCallAuthorizationContext context, CancellationToken ct = default)
    {
        if (context.Operation is not (SignalType.Initiate or SignalType.Answer)) return false;
        var caller = Identity(context.Caller);
        var recipient = Identity(context.Recipient);
        ActiveInvite active;
        lock (gate)
        {
            if (!invites.TryGetValue(context.CallId, out active!) || caller.Tenant != active.Tenant || recipient.Tenant != active.Tenant ||
                (!active.Answered && active.Invite.ExpiresAt <= DateTimeOffset.UtcNow) ||
                caller.User != active.Invite.CallerId || recipient.User != active.Invite.RecipientId ||
                context.CallerClientId != ClientId(context.CallId, caller.User) || context.RecipientClientId != ClientId(context.CallId, recipient.User) ||
                context.Caller.FindFirstValue("yap_call_id") != context.CallId.ToString() || context.Recipient.FindFirstValue("yap_call_id") != context.CallId.ToString()) return false;
        }
        await VerifyMembershipAsync(context.Caller, active.Invite.ThreadId, recipient.User, ct);
        await VerifyMembershipAsync(context.Recipient, active.Invite.ThreadId, caller.User, ct);
        lock (gate)
        {
            if (!invites.TryGetValue(context.CallId, out var current) || !ReferenceEquals(current, active) ||
                (!active.Answered && active.Invite.ExpiresAt <= DateTimeOffset.UtcNow)) return false;
            if (context.Operation == SignalType.Answer) active.Answered = true;
        }
        return true;
    }

    private async Task VerifyMembershipAsync(ClaimsPrincipal user, Guid thread, Guid other, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var client = new CommunicationsChatClient(services.GetRequiredService<ICommunicationsServiceWrapper>(),
            services.GetRequiredService<IConfiguration>(), new FixedActor(services.GetRequiredService<YapSessions>(), user),
            services.GetRequiredService<IActorAccessTokenScope>());
        var session = await client.ForCurrentActorAsync(ct: ct);
        var response = await session.GetThreadAsync(thread, ct);
        if (!response.IsSuccess || response.Response is null || !response.Response.Members.Any(x => x.CredentialId == session.CredentialId) ||
            !response.Response.Members.Any(x => x.CredentialId == other))
            throw new YapApiException(403, "Calls are available only to current conversation members.");
    }

    public static bool HasSameOrigin(HttpRequest request) => Uri.TryCreate(request.Headers.Origin.ToString(), UriKind.Absolute, out var origin) &&
        origin.Scheme == Uri.UriSchemeHttps && string.Equals(origin.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase) &&
        origin.AbsolutePath == "/" && string.IsNullOrEmpty(origin.Query) && string.IsNullOrEmpty(origin.Fragment) && string.IsNullOrEmpty(origin.UserInfo);

    private void RequireEnabled() { if (!Enabled) throw new YapApiException(503, "Voice calls are not available yet."); }
    private ActiveInvite GetInvite(Guid tenant, Guid user, Guid id) => invites.TryGetValue(id, out var value) && value.Tenant == tenant &&
        (value.Invite.CallerId == user || value.Invite.RecipientId == user) && (value.Answered || value.Invite.ExpiresAt > DateTimeOffset.UtcNow)
        ? value : throw new YapApiException(404, "This call has ended.");
    private void RemoveLocked(Guid id)
    {
        invites.Remove(id);
        foreach (var token in tickets.Where(x => x.Value.CallId == id).Select(x => x.Key).ToArray()) tickets.Remove(token);
    }
    private void PublishBoth(ActiveInvite invite, string type, Guid? credential = null)
    {
        var value = new YapCallEvent(type, invite.Invite, credential);
        Publish(invite.Tenant, invite.Invite.CallerId, value);
        Publish(invite.Tenant, invite.Invite.RecipientId, value);
    }
    private void Publish(Guid tenant, Guid user, YapCallEvent value)
    {
        Action<YapCallEvent>[] handlers;
        lock (gate) handlers = listeners.TryGetValue((tenant, user), out var list) ? list.ToArray() : [];
        foreach (var handler in handlers) { try { handler(value); } catch { /* A disconnected SSE subscriber cannot interrupt a call. */ } }
    }
    private void Prune()
    {
        ActiveInvite[] expired;
        lock (gate)
        {
            expired = invites.Values.Where(x => !x.Answered && x.Invite.ExpiresAt <= DateTimeOffset.UtcNow).ToArray();
            foreach (var item in expired) RemoveLocked(item.Invite.Id);
            foreach (var key in tickets.Where(x => x.Value.ExpiresAt <= DateTimeOffset.UtcNow).Select(x => x.Key).ToArray()) tickets.Remove(key);
        }
        foreach (var item in expired) { PublishBoth(item, "ended"); item.Lifetime.Cancel(); }
    }
    public void Dispose() { cleanup.Dispose(); lock (gate) foreach (var item in invites.Values) item.Lifetime.Cancel(); Server.Dispose(); }
    private sealed record Ticket(Guid CallId, Guid Tenant, Guid User, string Session, DateTimeOffset ExpiresAt);
    private sealed class ActiveInvite(Guid tenant, YapCallInvite invite)
    {
        public Guid Tenant { get; } = tenant;
        public YapCallInvite Invite { get; } = invite;
        public HashSet<Guid> Connected { get; } = [];
        public HashSet<Guid> Registered { get; } = [];
        public HashSet<Guid> Ready { get; } = [];
        public CancellationTokenSource Lifetime { get; } = new();
        public bool Answered { get; set; }
    }
    private sealed class FixedActor(YapSessions sessions, ClaimsPrincipal user) : ICommunicationsChatActorProvider
    { public async ValueTask<CommunicationsChatActor?> GetCurrentActorAsync(CancellationToken ct = default) => await sessions.GetActorAsync(user, ct); }
    private sealed class Subscription(Action dispose) : IDisposable { public void Dispose() => dispose(); }
    private sealed class ReadyTransport(IBoltConnection inner, Action ready) : IBoltConnection
    {
        private bool notified;
        public bool SupportsDatagrams => false;
        public bool IsConnected => inner.IsConnected;
        public BoltTransport TransportType => inner.TransportType;
        public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (!notified && data.Length >= 2 && data.Span[0] == (byte)FrameType.RegisterAck && data.Span[1] == 1) { notified = true; ready(); }
            await inner.SendAsync(data, ct);
        }
        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default) => inner.ReceiveAsync(buffer, ct);
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask CloseAsync(CancellationToken ct = default) => inner.CloseAsync(ct);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}

public static class YapCallEndpoints
{
    public static IServiceCollection AddYapCalls(this IServiceCollection services) => services.AddSingleton<YapCallGateway>();
    public static void MapYapCalls(this WebApplication app)
    {
        var api = app.MapGroup("/api/chat/calls").RequireAuthorization().AddEndpointFilter<YapApiFilter>();
        api.MapGet("/config", (YapCallGateway gateway) => new { enabled = gateway.Enabled, securityMode = "TrustedServerTls", groupCalls = false });
        api.MapPost("/", (StartYapCall request, HttpContext context, YapCallGateway gateway, CancellationToken ct) => gateway.StartAsync(context.User, request, ct));
        api.MapPost("/{id:guid}/connect", (Guid id, HttpContext context, YapCallGateway gateway, CancellationToken ct) => gateway.ConnectAsync(context.User, id, ct));
        api.MapPost("/{id:guid}/ready", (Guid id, HttpContext context, YapCallGateway gateway) => { gateway.Ready(context.User, id); return Results.NoContent(); });
        api.MapPost("/{id:guid}/end", (Guid id, HttpContext context, YapCallGateway gateway) => { gateway.End(context.User, id); return Results.NoContent(); });
        // The one-use ticket and exact Origin check protect the upgrade; the cookie is still required.
        // HTTP/2 WebSockets use extended CONNECT; HTTP/1.1 upgrades use GET.
        app.MapMethods("/api/chat/calls/socket", [HttpMethods.Get, HttpMethods.Connect], async (HttpContext context, YapCallGateway gateway, ILogger<YapCallGateway> logger) =>
        {
            try { await gateway.AcceptSocketAsync(context); }
            catch (YapApiException error) when (!context.Response.HasStarted)
            {
                // No URL/query, ticket, cookie, identity or audio is recorded.
                logger.LogWarning("Voice connection rejected. Status={Status} Reason={Reason}", error.Status, error.Message);
                context.Response.StatusCode = error.Status;
            }
        }).RequireAuthorization();
    }
}
