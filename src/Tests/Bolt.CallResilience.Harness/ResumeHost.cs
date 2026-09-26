// The harness's stand-in for YapCallGateway's resume contract (RESUME=1). The relay itself is the
// real BoltServer; this host only does what the gateway does around it:
//   - single-use tickets bound to the participant and to the seat generation they replace,
//   - a seat held for GRACE_S after its socket ends, instead of the call ending,
//   - a resume that supersedes a socket the server still believes is alive (an IP change),
//   - WebSocket keep-alive pings with a 20 s timeout, as on Yap's call sockets,
//   - the call ending (for the other side too) only once the hold runs out.
// The gateway's own implementation of these rules is covered by Yap.Tests; this harness measures
// what they do over a real, degraded TCP link.
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using Bolt.Protocol.Transport;
using Bolt.Server;

internal sealed class ResumeHost(BoltServer server, Guid call, Stopwatch clock, int graceSeconds)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, (string Id, int Generation, long Expires)> tickets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Seat> seats = new(StringComparer.Ordinal);
    private readonly List<object> holds = [];
    private int retired;
    private double T() => Math.Round(clock.Elapsed.TotalSeconds, 2);

    /// <summary>Set when a held seat ran out: the call is over for everyone.</summary>
    public string? Outcome { get; private set; }

    private sealed class Seat
    {
        public int Generation;
        public CancellationTokenSource? Connection;
        public TaskCompletionSource? Closed;
        public double? AwaySince;
        public long? HoldUntilMs;
        public bool Ended;
    }

    public void Map(WebApplication app)
    {
        app.MapGet("/ticket", (string id) =>
        {
            lock (gate)
            {
                var seat = SeatFor(id);
                if (seat.Ended) return Results.StatusCode(404);
                foreach (var old in tickets.Where(x => x.Value.Id == id).Select(x => x.Key).ToArray()) tickets.Remove(old);
                var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                tickets[ticket] = (id, seat.Generation, Environment.TickCount64 + 30_000);
                return Results.Text(ticket);
            }
        });
        app.Map("/ws-resume", AcceptAsync);
        app.MapGet("/ready", async (string id) =>
        {
            lock (gate) if (SeatFor(id).Ended) return Results.StatusCode(404);
            if (!await server.JoinGroupCallAsync(call, id)) return Results.StatusCode(503);
            lock (gate)
            {
                var seat = SeatFor(id);
                if (seat.AwaySince is { } since)
                {
                    Env.Log($"RESUMED t={T()} client={id} awayS={Math.Round(T() - since, 2)}");
                    holds.Add(new { client = id, awayAtS = since, resumedAtS = T() });
                }
                seat.AwaySince = null; seat.HoldUntilMs = null;
            }
            return Results.Ok();
        });
        _ = ExpireAsync();
        // Retirements by the stall watchdog are logged by the relay; count them for the summary.
        server.GroupParticipantRemoved += (_, id) => { if (id == "receiver") Interlocked.Increment(ref retired); };
    }

    private Seat SeatFor(string id) => seats.TryGetValue(id, out var seat) ? seat : seats[id] = new Seat();

    private async Task AcceptAsync(HttpContext context)
    {
        var token = context.Request.Query["ticket"].ToString();
        string id;
        int generation;
        CancellationTokenSource connection;
        TaskCompletionSource closed;
        CancellationTokenSource? superseded = null;
        Task? previous = null;
        lock (gate)
        {
            if (!tickets.Remove(token, out var ticket) || ticket.Expires < Environment.TickCount64) { context.Response.StatusCode = 403; return; }
            var seat = SeatFor(ticket.Id);
            if (seat.Ended || seat.Generation != ticket.Generation) { context.Response.StatusCode = 403; return; }
            id = ticket.Id;
            if (seat.Connection is { } live) { superseded = live; previous = seat.Closed?.Task; }
            generation = ++seat.Generation;
            connection = seat.Connection = new CancellationTokenSource();
            closed = seat.Closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (seat.AwaySince is not null) seat.HoldUntilMs = Math.Max(seat.HoldUntilMs ?? 0, Environment.TickCount64 + 30_000);
        }
        try
        {
            if (superseded is not null)
            {
                Env.Log($"SUPERSEDE t={T()} client={id} generation={generation}");
                superseded.Cancel();
                if (previous is not null) await previous.WaitAsync(TimeSpan.FromSeconds(10)).ContinueWith(_ => { });
            }
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, connection.Token);
            // A resumed socket gets the same tuning as the first one, as Yap's connect endpoint gives it.
            Relay.TuneSocket(context);
            using var socket = await context.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext
            { KeepAliveInterval = TimeSpan.FromSeconds(5), KeepAliveTimeout = TimeSpan.FromSeconds(20) });
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id), new Claim("bolt_media_client_id", id)], "harness"));
            Env.Log($"CONNECTED t={T()} client={id} generation={generation}");
            await server.HandleConnectionAsync(new WebSocketBoltConnection(socket), principal, lifetime.Token, isSecureTransport: true);
        }
        catch (Exception error) when (error is OperationCanceledException or WebSocketException or IOException) { }
        finally
        {
            closed.TrySetResult();
            lock (gate)
            {
                var seat = SeatFor(id);
                if (ReferenceEquals(seat.Connection, connection)) seat.Connection = null;
                if (seat.Generation == generation && !seat.Ended)
                {
                    // The socket ended; the seat does not. Grace counts from the first drop.
                    seat.AwaySince ??= T();
                    var until = Environment.TickCount64 + graceSeconds * 1000L;
                    seat.HoldUntilMs = seat.HoldUntilMs is { } held ? Math.Min(Math.Max(held, until), until + 30_000) : until;
                    Env.Log($"HOLD t={T()} client={id} graceS={graceSeconds}");
                }
            }
            connection.Dispose();
        }
    }

    private async Task ExpireAsync()
    {
        while (Outcome is null)
        {
            await Task.Delay(250);
            string? expired = null;
            lock (gate)
                foreach (var (id, seat) in seats)
                    if (!seat.Ended && seat.HoldUntilMs is { } until && until <= Environment.TickCount64)
                    { seat.Ended = true; expired = id; seat.Connection?.Cancel(); }
            if (expired is null) continue;
            Outcome = $"{expired} seat expired at {T()}s (connection lost)";
            Env.Log($"ENDED t={T()} client={expired} reason=connection-lost");
            await server.LeaveGroupCallAsync(call, expired);
        }
    }

    public object Summary()
    {
        lock (gate) return new { graceSeconds, holds = holds.ToArray(), relayRemovals = retired, ended = Outcome is not null };
    }

    /// <summary>The reason-carrying departure event, where the relay has one (newer relays only).</summary>
    public static bool TryWatchDepartures(BoltServer server, Action<string> log)
    {
        var departed = typeof(BoltServer).GetEvent("GroupParticipantDeparted");
        if (departed?.EventHandlerType is not { } type) return false;
        var parameter = type.GetMethod("Invoke")!.GetParameters()[0].ParameterType;
        var adapter = (Delegate)typeof(ResumeHost).GetMethod(nameof(Adapt), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(parameter).Invoke(null, [log])!;
        departed.AddEventHandler(server, adapter);
        return true;
    }

    private static Action<TDeparture> Adapt<TDeparture>(Action<string> log) => departure => log(departure?.ToString() ?? "");
}
