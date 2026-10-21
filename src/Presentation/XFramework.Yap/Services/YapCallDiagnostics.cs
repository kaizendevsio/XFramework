using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Features;
using Yap.Contracts;

namespace Yap.Services;

/// <summary>Temporary opt-in traces. No public read endpoint, database, or general log sink.</summary>
public sealed partial class YapCallDiagnostics(TimeProvider time, string? directory = null, string? enableFile = null) : BackgroundService
{
    public static readonly string Root = Path.Combine(Path.GetTempPath(), "yap-call-diagnostics");
    public static readonly string EnableFile = Path.Combine(Path.GetTempPath(), "yap-call-diagnostics.enabled");
    private readonly string root = directory ?? Root;
    private readonly string enablePath = enableFile ?? EnableFile;
    private readonly SemaphoreSlim gate = new(1);
    private readonly Dictionary<Guid, Session> sessions = [];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private sealed class Session(string owner, DateTimeOffset expires)
    {
        public string Owner { get; } = owner;
        public DateTimeOffset Expires { get; } = expires;
        public DateTimeOffset Last { get; set; }
        public int Sequence { get; set; } = -1;
        public int Count { get; set; }
        public bool Stopped { get; set; }
    }
    // Operator opens a two-hour collection window over SSH. It does not enable any client automatically.
    public bool Available => File.Exists(enablePath) &&
        time.GetUtcNow() - File.GetLastWriteTimeUtc(enablePath) is var age && age >= TimeSpan.FromSeconds(-5) && age < TimeSpan.FromHours(2);

    public async Task<CallDiagnosticSession?> StartAsync(string owner, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            Cleanup();
            if (!Available || sessions.Count >= 32 || sessions.Values.Count(x => x.Owner == owner && !x.Stopped && x.Expires > time.GetUtcNow()) >= 2) return null;
            var result = new CallDiagnosticSession(Guid.NewGuid(), time.GetUtcNow().AddMinutes(15));
            sessions.Add(result.Id, new(owner, result.ExpiresAt));
            return result;
        }
        finally { gate.Release(); }
    }

    public async Task<int> AppendAsync(Guid id, string owner, CallDiagnosticSample sample, CancellationToken ct)
    {
        if (!Valid(sample)) return 400;
        await gate.WaitAsync(ct);
        try
        {
            if (!sessions.TryGetValue(id, out var session) || session.Owner != owner) return 404;
            var now = time.GetUtcNow();
            if (!Available || session.Stopped || session.Expires <= now || session.Count >= 450) return 410;
            if (sample.Sequence <= session.Sequence) return 204; // An uncertain HTTP retry must not duplicate a sample.
            if (now - session.Last < TimeSpan.FromMilliseconds(1500)) return 429;
            Directory.CreateDirectory(root);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var path = Path.Combine(root, $"{id:N}.ndjson");
            var line = JsonSerializer.Serialize(new { sessionId = id, receivedAt = now, expiresAt = session.Expires, sample }, Json) + "\n";
            await File.AppendAllTextAsync(path, line, ct);
            session.Last = now; session.Sequence = sample.Sequence; session.Count++;
            return 204;
        }
        finally { gate.Release(); }
    }

    public async Task StopAsync(Guid id, string owner, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { if (sessions.TryGetValue(id, out var session) && session.Owner == owner) session.Stopped = true; }
        finally { gate.Release(); }
    }

    public static bool Valid(CallDiagnosticSample sample) => sample.CallId != Guid.Empty && sample.Sequence is >= 0 and <= 10000 &&
        Enum.IsDefined(sample.Device) && Enum.IsDefined(sample.CapturePath) && Enum.IsDefined(sample.Transport) && Enum.IsDefined(sample.RelayTransport) &&
        sample.Version is { Length: <= 24 } && VersionLabel().IsMatch(sample.Version) && ValidCodec(sample.Codec) &&
        ValidMetrics(sample.Metrics) && sample.Cuts is { Length: <= 8 } && sample.Cuts.All(ValidMetrics) && sample.Remotes is { Length: <= 8 } &&
        sample.Remotes.All(x => x is not null && x.StreamId != Guid.Empty && ValidCodec(x.Codec) && ValidMetrics(x.Metrics));
    private static bool ValidCodec(string? value) => value is { Length: <= 64 } && (value.Length == 0 || CodecLabel().IsMatch(value));
    private static bool ValidMetrics(Dictionary<CallMetric, double?>? metrics) => metrics is { Count: <= 90 } &&
        metrics.All(x => Enum.IsDefined(x.Key) && (x.Value is null || double.IsFinite(x.Value.Value) && Math.Abs(x.Value.Value) <= 1e12));
    [GeneratedRegex(@"^\d{1,4}\.\d{1,4}\.\d{1,4}$", RegexOptions.CultureInvariant)] private static partial Regex VersionLabel();
    [GeneratedRegex(@"^(h264|h265|hevc|vp8|vp9|av1|(avc1|hvc1|hev1|vp09|av01)(\.[A-Za-z0-9]{1,12}){1,8})$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)] private static partial Regex CodecLabel();

    private void Cleanup()
    {
        var now = time.GetUtcNow();
        foreach (var id in sessions.Where(x => x.Value.Expires.AddHours(1) <= now).Select(x => x.Key).ToArray()) sessions.Remove(id);
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.EnumerateFiles(root, "*.ndjson"))
            if (now - File.GetLastWriteTimeUtc(file) > TimeSpan.FromHours(1)) File.Delete(file);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await gate.WaitAsync(stoppingToken);
                try { Cleanup(); }
                catch (IOException) { /* Temporary files can also be removed by the operator. */ }
                finally { gate.Release(); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public static void Map(RouteGroupBuilder api)
    {
        var group = api.MapGroup("/call-diagnostics");
        // Enforce before JSON binding, including chunked bodies. The parent filter also checks account + CSRF.
        ((IEndpointConventionBuilder)group).Add(endpoint => endpoint.RequestDelegate = Limit(endpoint.RequestDelegate!));
        group.MapPost("/start", async (HttpContext ctx, YapCallDiagnostics store, CancellationToken ct) =>
            await store.StartAsync(Owner(ctx), ct) is { } session ? Results.Ok(session) : Results.StatusCode(503));
        group.MapPost("/{id:guid}", async (Guid id, CallDiagnosticSample sample, HttpContext ctx, YapCallDiagnostics store, CancellationToken ct) =>
            Results.StatusCode(await store.AppendAsync(id, Owner(ctx), sample, ct)));
        group.MapPost("/{id:guid}/stop", async (Guid id, HttpContext ctx, YapCallDiagnostics store, CancellationToken ct) =>
        { await store.StopAsync(id, Owner(ctx), ct); return Results.NoContent(); });
    }
    private static string Owner(HttpContext ctx) => $"{ctx.User.FindFirstValue(YapAuth.TenantClaim)}:{ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)}";
    private static RequestDelegate Limit(RequestDelegate next) => async ctx =>
    {
        if (ctx.Request.ContentLength > 16384) { ctx.Response.StatusCode = 413; return; }
        if (ctx.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size) size.MaxRequestBodySize = 16384;
        await next(ctx);
    };
}
