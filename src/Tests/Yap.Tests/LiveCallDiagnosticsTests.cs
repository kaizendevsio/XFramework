using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bolt.Media.Browser;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;
using Yap.Services;

namespace Yap.Tests;

public sealed class LiveCallDiagnosticsTests
{
    private string root = null!, enable = null!;
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    [SetUp] public void Setup() { root = Path.Combine(Path.GetTempPath(), "yap-diagnostic-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); enable = Path.Combine(root, "enabled"); }
    [TearDown] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private static CallDiagnosticSample Sample() => LiveCallDiagnostics.Project(new VideoDiagnostics
    {
        Capturing = true, Strategy = "worker-webgl", Codec = "avc1.42001e", CameraWidth = 360, CameraHeight = 640,
        Width = 640, Height = 360, EncodedFps = 20.8, Transport = "UDP/srflx (relay UDP/relay)",
        Remotes = [new() { StreamId = Guid.NewGuid(), Codec = "hvc1.1.6.L60.B0", RenderedFps = 0,
            WaitingForKeyframe = true, RecoveryRequests = 3,
            Recovery = new(Guid.NewGuid(), 67400, 81, 2489, 399, 372, 3, 23, 0, 3309, 350) }]
    }, Guid.NewGuid(), 1, CallDevice.Ios, true, 2000, 1080, 30);

    [Test] public async Task Store_EnforcesOwnershipRateExpiryAndBoundedSessions()
    {
        var clock = new Clock(); using var store = new YapCallDiagnostics(clock, root, enable);
        Assert.That(await store.StartAsync("a", default), Is.Null);
        File.WriteAllText(enable, "");
        var session = (await store.StartAsync("a", default))!;
        var sample = Sample();
        Assert.That(await store.AppendAsync(session.Id, "b", sample, default), Is.EqualTo(404));
        Assert.That(await store.AppendAsync(session.Id, "a", sample, default), Is.EqualTo(204));
        Assert.That(await store.AppendAsync(session.Id, "a", sample, default), Is.EqualTo(204));
        Assert.That(await store.AppendAsync(session.Id, "a", sample with { Sequence = 2 }, default), Is.EqualTo(429));
        Assert.That(File.ReadAllLines(Path.Combine(root, $"{session.Id:N}.ndjson")), Has.Length.EqualTo(1));
        await store.StopAsync(session.Id, "b", default);
        clock.Now += TimeSpan.FromSeconds(2);
        Assert.That(await store.AppendAsync(session.Id, "a", sample with { Sequence = 2 }, default), Is.EqualTo(204));
        await store.StopAsync(session.Id, "a", default);
        Assert.That(await store.AppendAsync(session.Id, "a", sample with { Sequence = 3 }, default), Is.EqualTo(410));
        var expiring = (await store.StartAsync("a", default))!;
        clock.Now += TimeSpan.FromMinutes(16);
        Assert.That(await store.AppendAsync(expiring.Id, "a", sample, default), Is.EqualTo(410));
        for (var i = 0; i < 30; i++) Assert.That(await store.StartAsync($"owner-{i}", default), Is.Not.Null);
        Assert.That(await store.StartAsync("overflow", default), Is.Null);
        clock.Now += TimeSpan.FromHours(1);
        Assert.That(await store.StartAsync("after-retention", default), Is.Not.Null);
        Assert.That(Directory.GetFiles(root, "*.ndjson"), Is.Empty);
    }

    [Test] public void Schema_RejectsFreeTextAndInvalidNumbers_PreservesDiagnosticEvidence()
    {
        var sample = Sample();
        Assert.That(YapCallDiagnostics.Valid(sample), Is.True);
        Assert.That(sample.CapturePath, Is.EqualTo(CallCapturePath.WorkerWebgl));
        Assert.That(sample.RelayTransport, Is.EqualTo(CallTransportPath.Udp));
        Assert.That(sample.Remotes[0].Metrics[CallMetric.LocalDrops], Is.EqualTo(81));
        Assert.That(sample.Remotes[0].Metrics[CallMetric.RenderedFps], Is.Zero);
        Assert.That(sample.Remotes[0].Metrics[CallMetric.WaitingForKeyframe], Is.EqualTo(1));
        Assert.That(sample.Remotes[0].Metrics[CallMetric.RecoveryRequests], Is.EqualTo(3));
        Assert.That(YapCallDiagnostics.Valid(sample with { Codec = "secret message" }), Is.False);
        Assert.That(YapCallDiagnostics.Valid(sample with { Version = "Bearer token" }), Is.False);
        Assert.That(YapCallDiagnostics.Valid(sample with { Device = (CallDevice)99 }), Is.False);
        Assert.That(YapCallDiagnostics.Valid(sample with { Metrics = new() { [(CallMetric)999] = 1 } }), Is.False);
        Assert.That(YapCallDiagnostics.Valid(sample with { Metrics = new() { [CallMetric.RttMs] = double.NaN } }), Is.False);
        Assert.That(YapCallDiagnostics.Valid(sample with { Metrics = null! }), Is.False);
        Assert.That(YapCallDiagnostics.Valid(sample with { Cuts = null! }), Is.False);
        Assert.That(YapCallDiagnostics.Valid(sample with { Cuts = [new() { [CallMetric.CutGradient] = double.NaN }] }), Is.False);
        Assert.That(YapCallDiagnostics.Valid(sample with { Cuts = Enumerable.Range(0, 9).Select(_ => new Dictionary<CallMetric, double?>()).ToArray() }), Is.False);
    }

    [Test] public void Schema_PreservesRouteAndEveryRecentCutWithoutAddressesOrCredentials()
    {
        var cut = new Bolt.Media.Congestion.RateCutSnapshot(1000, Bolt.Media.Congestion.RateDelaySource.Receiver,
            0, 12, 300, 30, 20, 2000, 0, 0, 100, 3000, 2400, Bolt.Media.Congestion.RateCause.StandingQueue);
        var sample = LiveCallDiagnostics.Project(new VideoDiagnostics
        {
            Topology = "peer-direct", Transport = "UDP/host", Codec = "avc1.420028",
            RateDetails = new Bolt.Media.Congestion.SendRateDecision(2400, 40, 2360, false, Bolt.Media.Congestion.RateSignal.Hold, 300)
            { LastCut = cut, Cuts = [cut], ReceiverAgeMs = 30, TransportAgeMs = 20 }
        }, Guid.NewGuid(), 1, CallDevice.Ios, true, 2000, 1080, 30);
        Assert.That(YapCallDiagnostics.Valid(sample), Is.True);
        Assert.That(sample.Metrics[CallMetric.Topology], Is.EqualTo(1));
        Assert.That(sample.Transport, Is.EqualTo(CallTransportPath.Udp));
        Assert.That(sample.Cuts.Single()[CallMetric.LastCutFromKbps], Is.EqualTo(3000));
        Assert.That(sample.Cuts.Single()[CallMetric.CutReceiverMs], Is.EqualTo(300));
    }

    [Test] public void Schema_PreservesWebGpuPreparationBreakdown()
    {
        var sample = LiveCallDiagnostics.Project(new VideoDiagnostics
        {
            Strategy = "worker-webgpu", CapturePaintMs = 2.5, CaptureFrameMs = 7.5,
            CapturePrepareMs = 10, Codec = "avc1.420028"
        }, Guid.NewGuid(), 1, CallDevice.Ios, true, 2000, 1080, 30);
        Assert.That(YapCallDiagnostics.Valid(sample), Is.True);
        Assert.That(sample.CapturePath, Is.EqualTo(CallCapturePath.WorkerWebgpu));
        Assert.That(sample.Metrics[CallMetric.CapturePaintMs], Is.EqualTo(2.5));
        Assert.That(sample.Metrics[CallMetric.CaptureFrameMs], Is.EqualTo(7.5));
    }

    [Test] public void Schema_PreservesDirectFrameExperimentAndHiddenOrientation()
    {
        var experiment = new FrameExperimentSample { Mode = 1, Width = 1920, Height = 1080,
            CameraWidth = 1080, CameraHeight = 1920, DecodedWidth = 1920, DecodedHeight = 1080,
            Rotation = null, Flip = null, PrepareMs = 0.1, PrepareP95Ms = 0.3, SnapshotMs = 0 };
        var sample = Sample() with { Metrics = experiment.Metrics(), Remotes = [] };
        Assert.That(YapCallDiagnostics.Valid(sample), Is.True);
        Assert.That(sample.Metrics[CallMetric.FrameExperiment], Is.EqualTo(1));
        Assert.That(sample.Metrics[CallMetric.FrameRotation], Is.Null, "Hidden orientation must not be reported as zero.");
        Assert.That(sample.Metrics[CallMetric.CameraWidth], Is.EqualTo(1080));
        Assert.That(sample.Metrics[CallMetric.RawWidth], Is.EqualTo(1920));
        Assert.That(sample.Metrics[CallMetric.DecodedWidth], Is.EqualTo(1920));
        Assert.That(sample.Metrics[CallMetric.CaptureFrameMs], Is.Zero);
    }

    [Test] public async Task Endpoint_RequiresSessionAccountCsrfAndBoundedBody_StoresReadableTrace()
    {
        File.WriteAllText(enable, "");
        using var store = new YapCallDiagnostics(TimeProvider.System, root, enable);
        await using var app = UiFixture.Create(0, configureServices: services => services.AddSingleton(store));
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new() }) { BaseAddress = new(app.Urls.Single()) };
        Assert.That((await client.PostAsJsonAsync("api/chat/call-diagnostics/start", new { })).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var login = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", login.AntiforgeryToken);
        (await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }))).EnsureSuccessStatusCode();
        login = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{login.User!.TenantId:N}:{login.User.CredentialId:N}");
        Assert.That((await client.PostAsJsonAsync("api/chat/call-diagnostics/start", new { })).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        client.DefaultRequestHeaders.Add("RequestVerificationToken", login.AntiforgeryToken);
        var response = await client.PostAsJsonAsync("api/chat/call-diagnostics/start", new { });
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<CallDiagnosticSession>())!;
        var route = $"api/chat/call-diagnostics/{session.Id}";
        var accepted = await client.PostAsJsonAsync(route, Sample());
        Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent), await accepted.Content.ReadAsStringAsync());
        using var record = JsonDocument.Parse(File.ReadAllLines(Path.Combine(root, $"{session.Id:N}.ndjson")).Single());
        Assert.That(record.RootElement.GetProperty("receivedAt").GetDateTimeOffset(), Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.That((await client.PostAsync(route, new StringContent(new string('x', 17000), System.Text.Encoding.UTF8, "application/json"))).StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        using var chunked = new HttpRequestMessage(HttpMethod.Post, route) { Content = new StringContent(new string('x', 17000), System.Text.Encoding.UTF8, "application/json") };
        chunked.Headers.TransferEncodingChunked = true;
        Assert.That((await client.SendAsync(chunked)).StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        client.DefaultRequestHeaders.Remove("X-Yap-Account");
        client.DefaultRequestHeaders.Add("X-Yap-Account", "another-account");
        Assert.That((await client.PostAsJsonAsync(route, Sample())).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        await app.StopAsync();
    }
}
