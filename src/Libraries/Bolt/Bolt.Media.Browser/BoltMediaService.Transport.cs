using Bolt.Client;

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
