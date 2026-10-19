using Bolt.Media.Browser;
using Microsoft.JSInterop;
using Yap.Contracts;
using M = Yap.Contracts.CallMetric;

namespace Yap.Client.Services;

/// <summary>Uploads a bounded, disposable sample; never queues or retries call telemetry.</summary>
public sealed class LiveCallDiagnostics(ChatApi api, IJSRuntime js) : IAsyncDisposable
{
    private CallDiagnosticSession? session;
    private IJSObjectReference? module;
    private CancellationTokenSource? upload;
    private string account = "";
    private long lastSent, started;
    private int sequence;
    private bool busy;
    private CallDevice device;
    public bool Enabled => session is not null && DateTimeOffset.UtcNow < session.ExpiresAt && account == api.Account;
    public string TestId => session?.Id.ToString("N")[..8] ?? "";
    private string status = "Not sharing";
    public string Status { get => session is not null && !Enabled ? "Sharing expired" : status; private set => status = value; }

    public async Task StartAsync()
    {
        module ??= await js.InvokeAsync<IJSObjectReference>("import", "./call-diagnostics.js");
        device = await module.InvokeAsync<CallDevice>("device");
        account = api.Account;
        session = await api.PostAsync<CallDiagnosticSession>("api/chat/call-diagnostics/start");
        started = Environment.TickCount64; lastSent = 0; sequence = 0;
        Status = "Ready for a call — sharing for up to 15 minutes";
    }
    public async Task StopAsync()
    {
        var old = session; session = null; upload?.Cancel(); Status = "Sharing stopped";
        if (old is not null)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await api.PostAsync<object>($"api/chat/call-diagnostics/{old.Id}/stop", ct: timeout.Token); } catch { }
        }
    }
    public void Offer(Guid callId, VideoDiagnostics snapshot, bool visible, int requestedHeight, int requestedFps)
    {
        if (!Enabled) { if (session is not null) { session = null; upload?.Cancel(); Status = "Sharing expired"; } return; }
        if (busy || Environment.TickCount64 - lastSent < 2000) return;
        lastSent = Environment.TickCount64;
        var sample = Project(snapshot, callId, ++sequence, device, visible, Environment.TickCount64 - started, requestedHeight, requestedFps);
        _ = SendAsync(session!.Id, sample);
    }
    private async Task SendAsync(Guid id, CallDiagnosticSample sample)
    {
        busy = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)); upload = timeout;
        try
        {
            await api.PostAsync<object>($"api/chat/call-diagnostics/{id}", sample, timeout.Token);
            if (session?.Id == id) Status = $"Live · sample {sample.Sequence} received";
        }
        catch (ChatApiException ex) when (ex.Status is 400 or 401 or 403 or 404 or 410 or 413)
        { if (session?.Id == id) { session = null; Status = "Sharing ended — enable it again in Settings"; } }
        catch { if (session?.Id == id) Status = "Sample skipped — waiting for connection"; }
        finally { if (ReferenceEquals(upload, timeout)) upload = null; busy = false; }
    }

    public static CallDiagnosticSample Project(VideoDiagnostics s, Guid call, int sequence, CallDevice device,
        bool visible, long elapsed, int height, int fps)
    {
        var rate = s.RateDetails;
        var metrics = new Dictionary<M, double?>
        {
            [M.Visible] = visible ? 1 : 0, [M.ElapsedMs] = elapsed, [M.RequestedHeight] = height, [M.RequestedFps] = fps,
            [M.Capturing] = s.Capturing ? 1 : 0, [M.CameraWidth] = s.CameraWidth, [M.CameraHeight] = s.CameraHeight,
            [M.CameraFps] = s.CameraFps, [M.Width] = s.Width, [M.Height] = s.Height, [M.TargetFps] = s.TargetFps,
            [M.CaptureFps] = s.CaptureFps, [M.EncodedFps] = s.EncodedFps, [M.EncodedKbps] = s.EncodedKbps,
            [M.EncoderQueue] = s.EncoderQueue, [M.SendQueue] = s.SendQueue, [M.EncoderDelayMs] = s.EncoderDelayMs,
            [M.CaptureDrops] = s.Dropped, [M.CapturePrepareMs] = s.CapturePrepareMs, [M.EncodedCopyMs] = s.EncodedCopyMs,
            [M.CapturePaintMs] = s.CapturePaintMs, [M.CaptureFrameMs] = s.CaptureFrameMs,
            [M.EventLoopLagMs] = s.EventLoopLagMs, [M.RttMs] = s.TransportRttMs, [M.UplinkResent] = s.UplinkResent,
            [M.TotalBudgetKbps] = s.TotalBudgetKbps, [M.VideoBudgetKbps] = s.VideoBudgetKbps,
            [M.AudioBitrateKbps] = s.AudioBitrateKbps, [M.QueueDelayMs] = s.QueueDelayMs, [M.VideoSuspended] = s.VideoSuspended ? 1 : 0,
            [M.LocalDelayMs] = rate?.LocalDelayMs, [M.TransportDelayMs] = rate?.TransportDelayMs,
            [M.RelayDelayMs] = rate?.RelayDelayMs, [M.ReceiverDelayMs] = rate?.ReceiverDelayMs,
            [M.UplinkLossPercent] = rate?.UplinkLossPercent, [M.CapacityKbps] = rate?.CapacityKbps,
            [M.RateCause] = rate is { } r ? (int)r.Cause : null, [M.LastCutCause] = rate is { } c ? (int)c.LastCutCause : null,
            [M.LastCutFromKbps] = rate?.LastCutFromKbps, [M.LastCutToKbps] = rate?.LastCutToKbps, [M.LastCutAgeMs] = rate?.LastCutAgeMs,
            [M.CryptoOperations] = s.EncryptionCosts?.Operations, [M.CryptoProcessingMs] = s.EncryptionCosts?.ProcessingMs,
            [M.CryptoWaitingMs] = s.EncryptionCosts?.WaitingMs
        };
        var path = s.Strategy switch
        { "processor" => CallCapturePath.Processor, "worker-processor" => CallCapturePath.WorkerProcessor,
            "worker-webgpu" => CallCapturePath.WorkerWebgpu,
            "worker-webgl" => CallCapturePath.WorkerWebgl, "rvfc" => CallCapturePath.Rvfc, "rvfc/webgl" => CallCapturePath.RvfcWebgl, _ => CallCapturePath.Unknown };
        var relay = s.Transport.IndexOf("(relay ", StringComparison.Ordinal);
        return new(call, sequence, device, AppRelease.Version, path, Transport(s.Transport),
            relay >= 0 ? Transport(s.Transport[(relay + 7)..]) : CallTransportPath.Unknown, s.Codec, metrics,
            s.Remotes.Take(8).Select(x => new CallDiagnosticRemote(x.StreamId, x.Codec, new()
            {
                [M.Width] = x.Width, [M.Height] = x.Height, [M.ReceivedFps] = x.ReceivedFps, [M.RenderedFps] = x.RenderedFps,
                [M.ReceivedKbps] = x.ReceivedKbps, [M.DecoderQueue] = x.DecoderQueue, [M.PendingFrames] = x.PendingFrames,
                [M.RenderMs] = x.RenderMs, [M.DecoderDelayMs] = x.DecoderDelayMs, [M.Resets] = x.Resets,
                [M.Fragments] = x.Recovery?.Fragments, [M.LocalDrops] = x.Recovery?.LocalDrops, [M.Nacked] = x.Recovery?.Nacked,
                [M.Recovered] = x.Recovery?.Recovered, [M.Abandoned] = x.Recovery?.Abandoned, [M.Declined] = x.Recovery?.Declined,
                [M.Incomplete] = x.Recovery?.Incomplete, [M.Skipped] = x.Recovery?.Skipped, [M.Pictures] = x.Recovery?.Pictures,
                [M.RecoveryMs] = x.Recovery?.RecoveryMs
            })).ToArray());
    }
    private static CallTransportPath Transport(string path) => path switch
    { var p when p.StartsWith("UDP", StringComparison.OrdinalIgnoreCase) => CallTransportPath.Udp,
      var p when p.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) => CallTransportPath.Tcp,
      var p when p.StartsWith("TLS", StringComparison.OrdinalIgnoreCase) => CallTransportPath.Tls,
      var p when p.StartsWith("WebSocket", StringComparison.OrdinalIgnoreCase) => CallTransportPath.WebSocket, _ => CallTransportPath.Unknown };
    public async ValueTask DisposeAsync() { await StopAsync(); if (module is not null) await module.DisposeAsync(); }
}
