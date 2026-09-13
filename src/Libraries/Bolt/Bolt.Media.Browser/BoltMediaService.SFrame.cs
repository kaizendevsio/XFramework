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

    public Task InstallSFrameEpochAsync(string epochId, string rosterHash, SFrameSenderKey local, IReadOnlyList<SFrameSenderKey> remote)
    {
        RequireSFrame();
        return _sframe!.InstallEpochAsync(epochId, rosterHash, local, remote);
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
