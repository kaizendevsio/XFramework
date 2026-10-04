namespace Bolt.Media.Browser;

public sealed partial class BoltMediaService
{
    public bool IsSFrameReady => _options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame && _sframe?.IsReady == true;

    public async Task ConfigureSFrameAsync(Guid callId, string localSenderId)
    {
        RequireSFrame();
        await _sframe!.ConfigureAsync(callId, localSenderId);
        _sframeLocalSenderId = localSenderId;
    }

    /// <summary>The media format every remote member of the installed epoch reads (<see cref="CallMediaFormat"/>).</summary>
    public int PeerMediaFormat { get; private set; } = CallMediaFormat.Legacy;

    /// <param name="peerMediaFormat">
    /// <see cref="CallMediaFormat.Common"/> of what the remote members announced in their authenticated envelopes. It
    /// decides whether this device sends compact SFrame frames and how long its Opus packets may be.
    /// </param>
    public async Task InstallSFrameEpochAsync(string epochId, string rosterHash, SFrameSenderKey local, IReadOnlyList<SFrameSenderKey> remote,
        int peerMediaFormat = CallMediaFormat.Legacy)
    {
        RequireSFrame();
        await _sframe!.InstallEpochAsync(epochId, rosterHash, local, remote, compact: peerMediaFormat >= CallMediaFormat.Compact);
        PeerMediaFormat = peerMediaFormat;
        // Fragments are sized by the overhead the format adds; the next picture measures the new one (from the
        // conservative default until then, so a switch to the larger legacy format never overflows a datagram).
        foreach (var id in new[] { _activeAudioStreamId, _activeVideoStreamId })
            if (id != Guid.Empty) _mediaClient?.GetMediaStream(id)?.ResetEncryptionOverhead();
        // A member that only plays 20 ms packets joined: the next tick shrinks them.
        if (_rateLoop is { } loop) loop.Audio.MaxFrameMs = CallMediaFormat.MaxAudioFrameMs(peerMediaFormat);
        // Orientation rides inside the SFrame plaintext, so it is only ever sent under an authenticated epoch, and only
        // when every member reads it; a member that does not turns the camera back to upright pixels.
        await _video.SetOrientationMetadataAsync(CallMediaFormat.SendsOrientation(peerMediaFormat));
    }

    public Task ActivateSFrameEpochAsync(string epochId, string rosterHash)
    {
        RequireSFrame();
        return _sframe!.ActivateEpochAsync(epochId, rosterHash);
    }

    public Task PauseSFrameAsync()
    {
        RequireSFrame();
        return _sframe!.PauseAsync();
    }

    /// <summary>Register local hosted-call state before gateway admission can replay remote stream configs.</summary>
    public Task JoinHostedGroupAsync(Guid callId)
    {
        EnsureInitialized(); RequireSFrame();
        if (_sframeLocalSenderId is null) throw new InvalidOperationException("Configure the SFrame call first.");
        _audio.OnEncoded -= OnAudioEncodedForStream;
        _audio.OnEncoded += OnAudioEncodedForStream;
        return _mediaClient!.JoinHostedGroupAsync(callId);
    }

    /// <summary>Publish local audio configuration only after this exact epoch has all-member acknowledgments.</summary>
    public Task StartHostedAudioAsync(Guid callId)
    {
        EnsureInitialized(); RequireSFrame();
        if (!IsSFrameReady) throw new InvalidOperationException("SFrame epoch is not active.");
        return HandleCallAnsweredAsync(callId);
    }

    private void RequireSFrame()
    {
        if (_options.SecurityMode != MediaSecurityMode.AuthenticatedSFrame || _sframe is null)
            throw new InvalidOperationException("Authenticated SFrame mode is not configured.");
    }
}
