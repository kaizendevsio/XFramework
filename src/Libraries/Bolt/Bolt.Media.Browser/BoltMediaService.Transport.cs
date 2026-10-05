using Bolt.Client;
using Bolt.Media.Congestion;

namespace Bolt.Media.Browser;

/// <summary>
/// Transport lifecycle for a resumable hosted call: detach from a lost connection without ending the
/// call, attach to the next one, and republish the local streams once the host has re-admitted this
/// device. Capture, encoders, the audio output and the SFrame session all survive the swap.
/// </summary>
public sealed partial class BoltMediaService
{
    /// <summary>The relay echoed a heartbeat; the value is the stamp this device sent.</summary>
    public event Action<long>? OnHeartbeatEcho;

    /// <summary><see cref="Environment.TickCount64"/> of the last frame from the relay on the current transport, or null while detached.</summary>
    public long? LastInboundTick => _mediaClient?.LastInboundTick;

    /// <summary>Whether a transport is attached (false between <see cref="SuspendTransportAsync"/> and <see cref="AttachTransport"/>).</summary>
    public bool HasTransport => _mediaClient is not null;

    private MediaTransportClient? _transport;

    /// <summary>The media path changed: a data channel opened or closed, or its route changed.</summary>
    public event Action<MediaPathStatus>? OnMediaPathChanged;

    /// <summary>Where media goes now: the data channel (and its route) or the WebSocket, and why.</summary>
    public MediaPathStatus MediaPath => _transport?.Status ?? new MediaPathStatus(MediaPathKind.WebSocket, "WebSocket",
        Reason: _datagramSupported ? "starting" : _options.DatagramTransport ? "unsupported" : "disabled");

    /// <summary>
    /// The device's network changed (online again, Wi-Fi to cellular). An open data channel restarts ICE in
    /// place; with none, the next attempt starts at once. The call's own resume handles a lost WebSocket.
    /// </summary>
    public void NetworkChanged() => _transport?.NetworkChanged();

    private MediaTransportClient? CreateDatagramTransport(BoltClient client, BoltMediaClient media)
    {
        if (!_datagramSupported || _rtc is not { } rtc) return null;
        var transport = new MediaTransportClient(client, rtc.CreatePeerAsync, _logger)
        {
            ReceiveLossPermille = media.SampleAudioReceiveLoss,
        };
        transport.StatusChanged += status =>
        {
            // The first time this transport's data channel opens, measure the link before media needs it.
            if (status.Kind == MediaPathKind.Datagram) StartLinkProbe(transport);
            OnMediaPathChanged?.Invoke(status);
        };
        // A channel that failed or stopped draining under the sender: what the rate control measured on it was the
        // dead channel, not the link. It starts over on the WebSocket instead of suspending video for a dead pipe.
        transport.PathLost += () => _rateLoop?.PathChanged();
        // The relay's per-message arrival times on this device's uplink: the rate loop's GCC-style delay-gradient and loss input.
        transport.TransportFeedback += signal => _signals.OnTransportFeedback(signal);
        // Requests a session once the socket is registered, and keeps the path healthy from then on.
        transport.Start();
        return transport;
    }

    // ── Start probe ──

    private MediaTransportClient? _probedTransport;
    private Task<LinkProbeResult?>? _probeTask;
    /// <summary>What this device's start probe found on the current transport, once it finished.</summary>
    public LinkProbeResult? LinkProbe { get; private set; }

    /// <summary>Where this call's picture started (or was last revised to while starting), and on what evidence.</summary>
    public StartEstimate? VideoStart => _rateLoop?.StartedAt;

    private void StartLinkProbe(MediaTransportClient transport)
    {
        if (!_options.StartProbe || ReferenceEquals(_probedTransport, transport)) return;
        _probedTransport = transport;
        LinkProbe = null;
        _probeTask = ProbeLinkAsync(transport);
    }

    private async Task<LinkProbeResult?> ProbeLinkAsync(MediaTransportClient transport)
    {
        try
        {
            var result = await transport.ProbeAsync(_options.StartProbeStepsKbps);
            if (result is null || !ReferenceEquals(_transport, transport)) return result;
            LinkProbe = result;
            // A picture that started before the probe finished takes the measurement now.
            _rateLoop?.UplinkMeasured(result);
            return result;
        }
        catch (Exception ex) { _logger.LogDebug(ex, "The start probe failed"); return null; }
    }

    /// <summary>
    /// What this device tells remote senders about its downlink (see BoltMediaClient.DownlinkReport): the start probe's
    /// measurement, else the browser's own ceiling for a slow connection, else nothing.
    /// </summary>
    private (uint Kbps, bool AtLeast)? DownlinkForReports()
    {
        if (LinkProbe is { DownlinkKbps: > 0 and var down } probe) return ((uint)down, probe.DownlinkAtLeast);
        if (_networkHint is { CapKbps: > 0 and var cap }) return ((uint)cap, false);
        return null;
    }

    private NetworkHint? _networkHint;

    /// <summary>
    /// Everything known about this path for a new picture: the start probe (waiting up to
    /// <see cref="MediaServiceOptions.StartProbeWaitMs"/> for a data channel still opening and its probe), and the
    /// browser's hints. A probe that lands later still revises the start (<see cref="PictureStart"/>).
    /// </summary>
    private async Task<StartHints> StartHintsAsync()
    {
        if (_options.StartProbe && _options.StartProbeWaitMs > 0 && LinkProbe is null)
        {
            var deadline = Environment.TickCount64 + _options.StartProbeWaitMs;
            // The channel is still opening (a callee turning the camera on as it connects): its probe is worth waiting for.
            while (_probeTask is null && _transport?.Status is { Kind: MediaPathKind.Negotiating } or { Kind: MediaPathKind.WebSocket, Reason: "starting" } &&
                   Environment.TickCount64 < deadline)
                await Task.Delay(50);
            if (_probeTask is { IsCompleted: false } running && deadline - Environment.TickCount64 is > 0 and var left)
            {
                try { await running.WaitAsync(TimeSpan.FromMilliseconds(left)); }
                catch (TimeoutException) { /* Start on the hints; the probe revises the rate when it lands. */ }
            }
        }
        _networkHint = await _audio.NetworkHintAsync() ?? _networkHint;
        var probe = LinkProbe;
        return new StartHints(probe?.UplinkKbps, probe?.UplinkAtLeast == true,
            CachedKbps: _networkHint is { CachedKbps: > 0 and var cached } ? cached : null,
            NetworkCapKbps: _networkHint is { CapKbps: > 0 and var cap } ? cap : null);
    }

    /// <summary>Send one liveness probe. False when there is no transport or it would not take the frame.</summary>
    public async Task<bool> SendHeartbeatAsync(Guid callId, long stamp)
    {
        if (_mediaClient is not { } client) return false;
        try { await client.SendHeartbeatAsync(callId, stamp); return true; }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ObjectDisposedException or OperationCanceledException)
        { return false; }
    }

    /// <summary>
    /// Let go of a transport that died. The microphone and camera stay open (their frames are simply
    /// not sent), the SFrame epoch and its sender counter continue, and the audio output stays
    /// unlocked; remote decoders are released because their streams belonged to the old connection.
    /// </summary>
    public async Task SuspendTransportAsync()
    {
        EnsureInitialized();
        var client = _mediaClient;
        _mediaClient = null;
        var datagram = _transport;
        _transport = null;
        _activeAudioStreamId = Guid.Empty;
        _activeVideoStreamId = Guid.Empty;
        lock (_configuredCalls) _configuredCalls.Clear();
        DrainVideoSend();
        // The pacer and rate loop belong to the lost transport (the pacer sends on its client, the controller's
        // delay and capacity history describe its path). Keep only the controller, for the resumed transport to
        // start from its stable rate; see StartSendPath.
        _resumeFrom = _rateLoop?.Controller ?? _resumeFrom;
        await StopSendPathAsync();

        var loops = _streamPlaybackTasks.Values.ToArray();
        foreach (var loop in loops)
        {
            try { await loop.Cancellation.CancelAsync(); }
            catch (ObjectDisposedException) { /* Already finished. */ }
        }
        try { await Task.WhenAll(loops.Select(loop => loop.Completion)).WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (TimeoutException) { /* A wedged decoder must not hold up the reconnect. */ }

        if (datagram is not null)
        {
            try { await datagram.DisposeAsync(); }
            catch (Exception ex) { _logger.LogDebug(ex, "Closing the lost datagram path failed"); }
        }
        if (client is not null)
        {
            try { await client.DisposeAsync(); }
            catch (Exception ex) { _logger.LogDebug(ex, "Detaching the lost call transport failed"); }
        }
    }

    /// <summary>
    /// Bind the next transport, before it connects, so that nothing the relay replays on admission
    /// (stream configurations of the other participants) arrives before its handlers exist.
    /// </summary>
    public void AttachTransport(BoltClient client)
    {
        EnsureInitialized();
        if (_mediaClient is not null) throw new InvalidOperationException("Suspend the current call transport first.");
        if (client.ServerUri.Scheme != "wss") throw new InvalidOperationException("Authenticated transport media requires a WSS endpoint.");
        _mediaClient = CreateMediaClient(client);
    }

    /// <summary>
    /// Publish the camera stream again after a resume, bound to the active SFrame epoch, and start it
    /// on a keyframe. The capture itself never stopped (unless the rate loop had suspended video for
    /// bandwidth, which the new path gets to try again); nothing reopens a camera the user turned off.
    /// The encoder moves to the picture the resumed rate fits before that keyframe, so the first
    /// picture on the new path is not one sized for the old path's peak.
    /// </summary>
    public async Task<bool> ResumeVideoStreamAsync(Guid callId)
    {
        EnsureInitialized();
        if (_mediaClient is not { } client || _activeVideoStreamId != Guid.Empty || _videoCodec == VideoCodec.None) return false;
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame && !IsSFrameReady) return false;
        StartSendPath();
        if (_adaptation is { } adaptation)
        {
            var wasSuspended = adaptation.Suspended;
            adaptation.Suspended = false;
            if (adaptation.Current is { } placed && await _video.ApplyTierAsync(placed)) _appliedTier = placed;
            if (wasSuspended && _videoLoop is not null && !_video.IsCapturing) await _video.StartCaptureAsync();
            OnVideoTierChanged?.Invoke(adaptation.Current);
        }
        await StartVideoStreamAsync(callId);
        var tier = _adaptation?.Current ?? VideoAdaptation.Ladder[Math.Clamp(_options.VideoStartTier, 0, VideoAdaptation.Ladder.Length - 1)];
        client.ConfigureVideoFeedback(_activeVideoStreamId, tier.BitrateKbps);
        _pacer?.ExpectKeyframe();
        // At once, past the encoder's one-per-second coalescing: receivers have nothing to decode until it lands.
        await _video.RequestKeyframeAsync(force: true);
        return true;
    }

    /// <summary>
    /// The send path is in trouble the way a person hears it: the queuing delay the rate controller acts on
    /// is above its own high-delay threshold, or it suspended the camera to keep the voice. The same
    /// thresholds drive the controller, so "Poor connection" and the rate never disagree about the link.
    /// </summary>
    public bool SendPathPoor => SendRate is { } rate && _rateLoop is { } loop &&
                                (rate.DelayMs >= loop.Controller.Options.HighDelayMs || _adaptation?.Suspended == true);
}
