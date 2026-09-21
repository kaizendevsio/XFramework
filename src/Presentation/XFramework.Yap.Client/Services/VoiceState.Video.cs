using Bolt.Media.Browser;
using Microsoft.AspNetCore.Components;
using Yap.Contracts;

namespace Yap.Client.Services;

/// <summary>One remote camera, already bound to the participant it belongs to.</summary>
public sealed record VideoTile(Guid StreamId, Guid CredentialId);

public sealed partial class VoiceState
{
    /// <summary>The server offers video for encrypted group calls.</summary>
    public bool VideoAvailable { get; private set; }
    /// <summary>This device's camera is on and sending. False unless the user turned it on.</summary>
    public bool CameraOn => active?.CameraOn == true;
    public bool CameraBusy => active?.CameraBusy == true;
    /// <summary>Why video is not running, in words the caller can act on. Never a device or endpoint detail.</summary>
    public string? VideoNotice { get; private set; }
    /// <summary>What the send ladder settled on, for the quality readout. Null while nothing is sent.</summary>
    public VideoTier? VideoQuality { get; private set; }
    public string VideoCodecName => active is { Codec: not VideoCodec.None } attempt
        ? VideoCodecLadder.Name(attempt.Codec).ToUpperInvariant() : "";

    /// <summary>
    /// Which camera is sending, "user" or "environment". The self-view mirrors the front camera
    /// because that is what a mirror does; mirroring the back one would reverse the text on
    /// whatever is being pointed at. What is sent is never mirrored either way.
    /// </summary>
    public string CameraFacing => active?.Facing is { Length: > 0 } facing ? facing : "user";

    /// <summary>Remote cameras currently publishing, newest roster order.</summary>
    public IReadOnlyList<VideoTile> RemoteVideo => active?.Tiles ?? [];
    public bool AnyVideo => CameraOn || RemoteVideo.Count != 0;

    /// <summary>Start a call and open the camera as soon as its keys are active.</summary>
    public Task StartVideoCallAsync(Guid thread, string name, IReadOnlyList<Person> people)
        => StartGroupAsync(thread, name, people, video: true);

    /// <summary>
    /// Turn this device's camera on or off.
    ///
    /// Nothing else in the client opens a camera: answering a call, reconnecting and roster changes
    /// all leave it closed. Turning it off releases the capture device, so the platform's camera
    /// indicator goes out rather than staying lit for the rest of the call.
    /// </summary>
    public async Task ToggleCameraAsync()
    {
        var attempt = active;
        if (attempt?.Group is null || attempt.Media is not { } media || !Current(attempt) || attempt.CameraBusy) return;
        if (!attempt.CameraOn && (ConnectedAt is null || attempt.Epoch?.Active != true)) return;
        attempt.CameraBusy = true; VideoNotice = null; Notify();
        try
        {
            if (attempt.CameraOn) await StopCameraAsync(attempt, null);
            else await StartCameraAsync(attempt, media);
        }
        catch (Exception error) when (Current(attempt))
        {
            await StopCameraAsync(attempt, CameraMessage(error));
        }
        finally { attempt.CameraBusy = false; if (Current(attempt)) Notify(); }
    }

    /// <summary>Flip between the front and back camera on a phone.</summary>
    public Task SwitchCameraAsync() => UseCameraAsync(null, active?.Facing == "user" ? "environment" : "user");

    /// <summary>The camera this device is sending from, and the ones it could switch to.</summary>
    public string CameraDeviceId => active?.Device ?? "";
    public async Task<IReadOnlyList<MediaDeviceInfo>> CamerasAsync()
    {
        // Labels only exist once camera permission has been granted, so this is worth calling
        // after the camera is on, not before.
        try { return active?.Media is { } media ? await media.CamerasAsync() : []; } catch { return []; }
    }

    /// <summary>Pick a specific camera. A laptop with two webcams needs this; a phone uses the flip.</summary>
    public Task SelectCameraAsync(string deviceId) => UseCameraAsync(deviceId, null);

    private async Task UseCameraAsync(string? deviceId, string? facing)
    {
        var attempt = active;
        if (attempt?.Media is not { } media || !attempt.CameraOn || attempt.CameraBusy || !Current(attempt)) return;
        attempt.CameraBusy = true; Notify();
        try
        {
            var state = await media.StartVideoAsync(attempt.Group!.Id, attempt.Codec, attempt.CodecCeiling, deviceId, facing);
            attempt.Facing = state.FacingMode; attempt.Device = state.DeviceId;
        }
        catch (Exception error) when (Current(attempt)) { await StopCameraAsync(attempt, CameraMessage(error)); }
        finally { attempt.CameraBusy = false; if (Current(attempt)) Notify(); }
    }

    public async Task AttachLocalPreviewAsync(ElementReference element)
    {
        if (active?.Media is { } media && CameraOn) { try { await media.AttachLocalPreviewAsync(element); } catch { /* The tile went away mid-render. */ } }
    }

    public async Task AttachRemoteVideoAsync(Guid streamId, ElementReference canvas)
    {
        if (active?.Media is { } media) { try { await media.AttachRemoteVideoAsync(streamId, canvas); } catch { /* Same. */ } }
    }

    // ── Internals ──

    private async Task StartCameraAsync(Attempt attempt, BoltMediaService media)
    {
        if (attempt.Codec == VideoCodec.None)
        { VideoNotice = attempt.CodecNotice ?? VideoBlockedNotice(attempt.VideoBlocked, attempt.Ladder?.Probed.Any(x => x.Encode) == true); return; }
        if (attempt.Tiles.Count >= Bolt.Media.Browser.VideoAdaptation.MaxVideoParticipants)
        { VideoNotice = "This call already has as many cameras as it can carry."; return; }
        // The roster is claimed before the camera opens: a refusal must not leave a lit camera behind.
        await api.PostAsync($"api/chat/calls/groups/{attempt.Group!.Id}/video", new YapGroupVideo(true), attempt.Lifetime.Token);
        CheckCurrent(attempt);
        media.OnLocalVideoStopped -= attempt.VideoStopped;
        media.OnLocalVideoStopped += attempt.VideoStopped;
        media.OnVideoTierChanged -= attempt.TierChanged;
        media.OnVideoTierChanged += attempt.TierChanged;
        var state = await media.StartVideoAsync(attempt.Group.Id, attempt.Codec, attempt.CodecCeiling, null, attempt.Facing);
        if (!Current(attempt)) { await media.StopVideoAsync(); return; }
        attempt.Facing = state.FacingMode; attempt.Device = state.DeviceId;
        attempt.CameraOn = true;
        VideoQuality = media.ActiveVideoTier;
        await media.SetVideoParticipantsAsync(attempt.Tiles.Count + 1);
    }

    private async Task StopCameraAsync(Attempt attempt, string? notice)
    {
        attempt.CameraOn = false;
        VideoQuality = null;
        if (notice is not null) VideoNotice = notice;
        if (attempt.Media is { } media) { try { await media.StopVideoAsync(); } catch { /* The page may already be tearing down. */ } }
        if (attempt.Group is null || !Current(attempt)) return;
        try { await api.PostAsync($"api/chat/calls/groups/{attempt.Group.Id}/video", new YapGroupVideo(false), attempt.Lifetime.Token); }
        catch { /* The roster flag is cosmetic; the camera is already released. */ }
    }

    /// <summary>The browser released the camera by itself: page hidden, permission revoked, encoder gone.</summary>
    private void CameraReleased(Attempt attempt, string reason)
    {
        if (!Current(attempt) || !attempt.CameraOn) return;
        _ = InvokeStopAsync(attempt, reason switch
        {
            // Backgrounding must free the camera; saying so beats a silently black tile on return.
            "hidden" => "Camera turned off while Yap was in the background.",
            "denied" => "Yap could not use the camera. Check its permission for this site.",
            "ended" => "The camera was disconnected.",
            _ => "The camera stopped unexpectedly."
        });
    }

    private async Task InvokeStopAsync(Attempt attempt, string notice)
    {
        try { await StopCameraAsync(attempt, notice); } catch { /* Best effort; the camera is already stopped. */ }
        if (Current(attempt)) Notify();
    }

    private static string CameraMessage(Exception error) => error switch
    {
        ChatApiException { Status: 409 } => "This call already has as many cameras as it can carry.",
        ChatApiException { Status: 503 } => "Video calls are not available yet.",
        _ => "The camera could not start. Check camera access for this site."
    };

    /// <summary>
    /// Decide the wire codec for this epoch from what every accepted peer said it can decode.
    /// AV1 first, then VP9, then H.264: the same picture costs roughly a third less on AV1 than on
    /// H.264, but only a device with a hardware encoder is allowed to choose it at the larger sizes.
    /// </summary>
    private void NegotiateVideo(Attempt attempt, GroupEpoch epoch)
    {
        if (!VideoAvailable || attempt.Ladder is not { } ladder) { attempt.Codec = VideoCodec.None; return; }
        var height = Math.Min(attempt.Ceiling,
            Bolt.Media.Browser.VideoAdaptation.HeightCapForParticipants(epoch.Peers.Length + 1));
        var codec = ladder.Negotiate(epoch.Peers.Select(peer => epoch.PeerCodecs.GetValueOrDefault(peer, [])), height);
        // Fall back through the ladder before giving up: a peer without AV1 should still get VP9 or H.264.
        if (codec == VideoCodec.None && height > 360)
            codec = ladder.Negotiate(epoch.Peers.Select(peer => epoch.PeerCodecs.GetValueOrDefault(peer, [])), 360);
        attempt.CodecCeiling = Math.Min(height, ladder.EncodingCeiling(codec));
        var previous = attempt.Codec;
        attempt.Codec = codec;
        attempt.CodecNotice = codec == VideoCodec.None
            ? VideoBlockedNotice(attempt.VideoBlocked, ladder.Probed.Any(x => x.Encode))
            : null;
        // A codec that stops working mid-call means a late joiner cannot decode it. Stop rather than
        // keep sending a picture that one participant only sees as a frozen tile.
        if (previous != VideoCodec.None && codec != previous && attempt.CameraOn)
            _ = InvokeStopAsync(attempt, "Video stopped: a participant's device cannot receive this video.");
    }

    /// <summary>
    /// Why video is off, in words that point at the actual fix.
    ///
    /// "This device cannot encode video" reads as a hardware limit and sends nobody anywhere useful.
    /// A browser without the capture or codec API is the commonest cause by far - it is what every
    /// iOS Safari hit before the frame-callback path existed - and the fix is a different browser,
    /// so <paramref name="browserReason"/> is kept rather than overwritten by the codec outcome.
    /// </summary>
    internal static string VideoBlockedNotice(string? browserReason, bool anyLocalEncoder) =>
        browserReason ?? (anyLocalEncoder
            ? "No video format works for everyone on this call."
            : "This device has no video encoder for calls.");

    private void ApplyRemoteVideo(Attempt attempt)
    {
        if (attempt.Media is not { } media || !Current(attempt)) return;
        var tiles = media.RemoteVideo
            .Select(x => new VideoTile(x.StreamId, CredentialOf(x.SenderId)))
            .Where(x => x.CredentialId != Guid.Empty).ToArray();
        attempt.Tiles = tiles;
        _ = media.SetVideoParticipantsAsync(tiles.Length + (attempt.CameraOn ? 1 : 0));
        Notify();
    }

    /// <summary>Media sender IDs are "yap-media-{call}-{credential}"; the tail names the participant.</summary>
    internal static Guid CredentialOf(string senderId) =>
        senderId.Length >= 32 && Guid.TryParseExact(senderId[^32..], "N", out var credential) ? credential : Guid.Empty;
}
