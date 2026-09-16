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

/// <summary>Authenticated Yap voice admission and media relay; encrypted groups forward client-encrypted SFrame audio.</summary>
public sealed partial class YapCallGateway : IBoltCallAuthorizer, IBoltGroupCallAuthorizer, IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, ActiveInvite> invites = [];
    private readonly Dictionary<string, Ticket> tickets = new(StringComparer.Ordinal);
    private readonly Dictionary<(Guid Tenant, Guid User), List<Action<YapCallEvent>>> listeners = [];
    private readonly IServiceScopeFactory scopes;
    private readonly ILogger pushLogger;
    private readonly Timer cleanup;

    /// <summary>How long a ringing invite stays valid; push TTLs are matched to it.</summary>
    internal static readonly TimeSpan InviteLifetime = TimeSpan.FromSeconds(60);
    public bool Enabled { get; }
    public bool EncryptedGroupsEnabled => Enabled && groupLifecycleEnabled;
    public BoltServer Server { get; }

    public YapCallGateway(IConfiguration configuration, IServiceScopeFactory scopes, ILogger<BoltServer> logger)
        : this(configuration, scopes, logger, configuration.GetValue<bool>("Yap:Calls:EncryptedGroups")) { }

    // Kept explicit for disposable fixtures; production uses the default-false configuration gate.
    internal YapCallGateway(IConfiguration configuration, IServiceScopeFactory scopes, ILogger<BoltServer> logger, bool enableGroupLifecycle)
    {
        this.scopes = scopes;
        pushLogger = logger;
        groupLifecycleEnabled = enableGroupLifecycle;
        Enabled = configuration.GetValue<bool>("Yap:Calls:Enabled");
        var requiredMode = enableGroupLifecycle ? "EndToEndEncrypted" : "TrustedServerTls";
        if (Enabled && configuration["Yap:Calls:SecurityMode"] != requiredMode)
            throw new InvalidOperationException($"Yap calls require the explicit {requiredMode} security mode.");
        Server = new BoltServer(logger, new BoltServerOptions
        {
            MediaEnabled = Enabled, RequireSecureTransport = true, AuthenticatedMediaOnly = true,
            RequireEncryptedMedia = enableGroupLifecycle,
            CallAuthorizer = this, MaxActiveCalls = 64, MaxActiveCallsPerPrincipal = 1,
            GroupCallAuthorizer = enableGroupLifecycle ? this : null,
            MaxCallParticipants = enableGroupLifecycle ? 8 : 2, MaxMediaStreamsPerPrincipal = 2,
            MaxFrameBytes = 64 * 1024, SendQueueCapacity = 64, SendQueueByteCapacity = 1024 * 1024,
            SendEnqueueTimeoutMs = enableGroupLifecycle ? 250 : 0,
            MaxConnectionsPerPrincipal = 2, MaxConnectionLifetimeSeconds = 3600
        });
        Server.GroupParticipantRemoved += GroupParticipantRemoved;
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
            user.Identity?.Name ?? "Someone", request.RecipientId, DateTimeOffset.UtcNow.Add(InviteLifetime));
        lock (gate)
        {
            if (invites.Count + groups.Count >= 64 || GroupMemberBusy(tenant, caller) || GroupMemberBusy(tenant, request.RecipientId) || invites.Values.Any(x => x.Tenant == tenant &&
                (x.Invite.CallerId == caller || x.Invite.RecipientId == caller ||
                 x.Invite.CallerId == request.RecipientId || x.Invite.RecipientId == request.RecipientId)))
                throw new YapApiException(409, "One of you is already in a call.");
            invites.Add(invite.Id, new ActiveInvite(tenant, invite));
        }
        Publish(tenant, invite.RecipientId, new("incoming", invite));
        // A closed or backgrounded device has no live subscriber; push is the only way it rings.
        NotifyIncomingCall(tenant, invite.ThreadId, invite.Id, invite.ExpiresAt, [invite.RecipientId]);
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
            ReplayGroupsLocked(key.Tenant, key.User, handler);
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
        bool groupTicket;
        lock (gate) groupTicket = tickets.TryGetValue(token, out var candidate) && candidate.Group;
        if (groupTicket) { await AcceptGroupSocketAsync(context, token); return; }
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
        if (!(await CurrentMembersAsync(user, thread, ct)).Contains(other))
            throw new YapApiException(403, "Calls are available only to current conversation members.");
    }

    private async Task<HashSet<Guid>> CurrentMembersAsync(ClaimsPrincipal user, Guid thread, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var client = new CommunicationsChatClient(services.GetRequiredService<ICommunicationsServiceWrapper>(),
            services.GetRequiredService<IConfiguration>(), new FixedActor(services.GetRequiredService<YapSessions>(), user),
            services.GetRequiredService<IActorAccessTokenScope>());
        var session = await client.ForCurrentActorAsync(ct: ct);
        var response = await session.GetThreadAsync(thread, ct);
        if (!response.IsSuccess || response.Response is null || !response.Response.Members.Any(x => x.CredentialId == session.CredentialId))
            throw new YapApiException(403, "Calls are available only to current conversation members.");
        return response.Response.Members.Select(x => x.CredentialId).ToHashSet();
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
        PruneGroups();
        ActiveInvite[] expired;
        lock (gate)
        {
            expired = invites.Values.Where(x => !x.Answered && x.Invite.ExpiresAt <= DateTimeOffset.UtcNow).ToArray();
            foreach (var item in expired) RemoveLocked(item.Invite.Id);
            foreach (var key in tickets.Where(x => x.Value.ExpiresAt <= DateTimeOffset.UtcNow).Select(x => x.Key).ToArray()) tickets.Remove(key);
        }
        foreach (var item in expired) { PublishBoth(item, "ended"); item.Lifetime.Cancel(); }
    }
    public void Dispose()
    {
        cleanup.Dispose();
        Server.GroupParticipantRemoved -= GroupParticipantRemoved;
        lock (gate)
        {
            foreach (var item in invites.Values) item.Lifetime.Cancel();
            foreach (var room in groups.Values) foreach (var member in room.Members.Values) member.Lifetime.Cancel();
        }
        Server.Dispose();
    }
    private sealed record Ticket(Guid CallId, Guid Tenant, Guid User, string Session, DateTimeOffset ExpiresAt, bool Group = false);
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
        api.MapGet("/config", (YapCallGateway gateway) => new { enabled = gateway.Enabled,
            securityMode = gateway.EncryptedGroupsEnabled ? "EndToEndEncrypted" : "TrustedServerTls", groupCalls = gateway.EncryptedGroupsEnabled });
        api.MapPost("/", (StartYapCall request, HttpContext context, YapCallGateway gateway, CancellationToken ct) => gateway.StartAsync(context.User, request, ct));
        api.MapPost("/{id:guid}/connect", (Guid id, HttpContext context, YapCallGateway gateway, CancellationToken ct) => gateway.ConnectAsync(context.User, id, ct));
        api.MapPost("/{id:guid}/ready", (Guid id, HttpContext context, YapCallGateway gateway) => { gateway.Ready(context.User, id); return Results.NoContent(); });
        api.MapPost("/{id:guid}/end", (Guid id, HttpContext context, YapCallGateway gateway) => { gateway.End(context.User, id); return Results.NoContent(); });
        // Admission stays disabled in the production constructor until encrypted group audio is verified.
        api.MapPost("/groups", (StartYapGroupCall request, HttpContext context, YapCallGateway gateway, CancellationToken ct) =>
        {
            if (request.DeviceId == Guid.Empty || request.Recipients is null) throw new YapApiException(400, "An approved device is required.");
            return gateway.StartGroupAsync(context.User, request.ThreadId, request.Recipients, ct, request.DeviceId);
        });
        api.MapGet("/groups/{id:guid}", (Guid id, HttpContext context, YapCallGateway gateway) => gateway.GroupRoster(context.User, id));
        api.MapPost("/groups/{id:guid}/accept", (Guid id, AcceptYapGroupCall request, HttpContext context, YapCallGateway gateway, CancellationToken ct) =>
        {
            if (request.DeviceId == Guid.Empty) throw new YapApiException(400, "An approved device is required.");
            return gateway.AcceptGroupAsync(context.User, id, ct, request.DeviceId);
        });
        api.MapPost("/groups/{id:guid}/connect", (Guid id, HttpContext context, YapCallGateway gateway, CancellationToken ct) => gateway.ConnectGroupAsync(context.User, id, ct));
        api.MapPost("/groups/{id:guid}/ready", async (Guid id, YapGroupReady request, HttpContext context, YapCallGateway gateway, CancellationToken ct) => { await gateway.ReadyGroupAsync(context.User, id, ct, request.Revision); return Results.NoContent(); });
        api.MapPost("/groups/{id:guid}/leave", async (Guid id, HttpContext context, YapCallGateway gateway, CancellationToken ct) => { await gateway.LeaveGroupAsync(context.User, id, ct); return Results.NoContent(); });
        api.MapPost("/groups/{id:guid}/control", async (Guid id, YapGroupControl request, HttpContext context, YapCallGateway gateway, CancellationToken ct) => { await gateway.RelayGroupControlAsync(context.User, id, request, ct); return Results.NoContent(); });
        api.MapPost("/groups/{id:guid}/mute", (Guid id, YapGroupMute request, HttpContext context, YapCallGateway gateway) => { gateway.MuteGroup(context.User, id, request.Muted); return Results.NoContent(); });
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
