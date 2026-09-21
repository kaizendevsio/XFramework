using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Bolt.Client;
using Bolt.Media.Browser;
using Microsoft.Extensions.Logging;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class VoiceState
{
    public IReadOnlyList<YapGroupParticipant> Participants => active?.Group?.Participants.Where(x => !x.Left).ToArray() ?? [];
    public string ParticipantName(Guid credential) => credential == chat.User?.CredentialId ? "You" :
        chat.Selected?.People.FirstOrDefault(x => x.Id == credential)?.Name ?? "Participant";
    public string? ParticipantAvatar(Guid credential) => chat.Selected?.People.FirstOrDefault(x => x.Id == credential)?.AvatarUrl;

    public Task StartGroupAsync(Guid thread, string name, IReadOnlyList<Person> people, bool video = false)
    {
        if (!Enabled || disposed || active is not null || chat.User is null || chat.NeedsLogin || api.Account != chat.Scope)
            return Task.CompletedTask;
        var recipients = people.Where(x => x.Id != chat.User.CredentialId).Select(x => x.Id).Distinct().ToArray();
        if (recipients.Length is < 1 or > 7) { Error = "Voice calls support up to eight people."; Notify(); return Task.CompletedTask; }
        var attempt = active = new Attempt(chat.Scope) { Starting = true, WantsVideo = video && VideoAvailable };
        Name = name; AvatarUrl = people.Count == 1 ? people[0].AvatarUrl : null;
        Status = "Preparing microphone..."; Incoming = false; Minimized = false; Error = null;
        VideoQuality = null; VideoNotice = null; Notify();
        return attempt.Setup = StartEncryptedGroupCoreAsync(attempt, thread, recipients);
    }

    private Guid ApprovedDevice() => chat.Encryption.Status.Approved && chat.Encryption.LocalDeviceId is { } device
        ? device : throw new InvalidOperationException("Approve this device in Settings before calling.");

    private async Task StartEncryptedGroupCoreAsync(Attempt attempt, Guid thread, Guid[] recipients)
    {
        try
        {
            var device = ApprovedDevice();
            await PrepareAsync(attempt);
            var group = await api.PostAsync<YapGroupCall>("api/chat/calls/groups", new StartYapGroupCall(thread, device, recipients, attempt.WantsVideo), attempt.Lifetime.Token);
            attempt.Group = group;
            attempt.Invite = InviteFor(group);
            CheckCurrent(attempt);
            await ConnectEncryptedGroupAsync(attempt);
        }
        catch (OperationCanceledException) when (!Current(attempt)) { }
        catch (Exception error) { await FailAsync(attempt, error); }
        finally { attempt.Starting = false; if (Current(attempt)) Notify(); }
    }

    private async Task AcceptGroupCoreAsync(Attempt attempt)
    {
        try
        {
            var device = ApprovedDevice();
            await PrepareAsync(attempt);
            var group = attempt.Group ?? throw new InvalidOperationException("The call has ended.");
            attempt.Group = await api.PostAsync<YapGroupCall>($"api/chat/calls/groups/{group.Id}/accept", new AcceptYapGroupCall(device), attempt.Lifetime.Token);
            CheckCurrent(attempt);
            await ConnectEncryptedGroupAsync(attempt);
        }
        catch (OperationCanceledException) when (!Current(attempt)) { }
        catch (Exception error) { await FailAsync(attempt, error); }
        finally { attempt.Starting = false; if (Current(attempt)) Notify(); }
    }

    private async Task ConnectEncryptedGroupAsync(Attempt attempt)
    {
        var group = attempt.Group!;
        var media = attempt.Media!;
        // Download/initialize crypto before minting the 30-second, single-use socket ticket.
        // A cold mobile connection can take longer than that to load the WASM module.
        attempt.Phase = "encryption-runtime";
        var sender = MediaSender(group.Id, chat.User!.CredentialId);
        await media.ConfigureSFrameAsync(group.Id, sender);
        CheckCurrent(attempt);
        attempt.Phase = "connection-ticket";
        var connection = await api.PostAsync<YapCallConnection>($"api/chat/calls/groups/{group.Id}/connect", ct: attempt.Lifetime.Token)
            ?? throw new InvalidOperationException("The call connection was not supplied.");
        CheckCurrent(attempt);
        if (connection.ClientId != sender) throw new InvalidOperationException("Unexpected call identity.");
        attempt.Connection = connection;
        var endpoint = navigation.ToAbsoluteUri(connection.Url);
        var origin = navigation.ToAbsoluteUri("/");
        if (origin.Scheme != "https" || endpoint.Scheme != "https" || endpoint.Authority != origin.Authority)
            throw new InvalidOperationException("Voice calls require a secure connection to Yap.");
        var client = attempt.Client = new BoltClient(new UriBuilder(endpoint) { Scheme = "wss" }.Uri,
            connection.ClientId, "Yap encrypted voice", new BoltClientOptions { MinConnections = 1, MaxConnections = 1, MaxFrameBytes = 65536 }, logs.CreateLogger("Yap.Voice"));
        attempt.Disconnected = () => { if (Current(attempt)) _ = EndAfterCallbackAsync(attempt); };
        client.Disconnected += attempt.Disconnected;
        await media.InitializeAsync(client);
        CheckCurrent(attempt);
        attempt.Phase = "call-transport";
        await client.ConnectAsync(attempt.Lifetime.Token);
        CheckCurrent(attempt);
        await media.JoinHostedGroupAsync(group.Id);
        CheckCurrent(attempt);
        attempt.VideoStopped = reason => CameraReleased(attempt, reason);
        attempt.TierChanged = tier =>
        {
            if (!Current(attempt)) return;
            VideoQuality = tier;
            // A null tier is the ladder standing video down so the voice keeps its bandwidth.
            if (attempt.CameraOn) VideoNotice = tier is null ? "Video paused: not enough bandwidth. Audio continues." : null;
            Notify();
        };
        attempt.RemoteVideoChanged = () => _ = InvokeRemoteVideoAsync(attempt);
        media.OnRemoteVideoChanged += attempt.RemoteVideoChanged;
        attempt.TransportReady = true;
        // Acceptance may race the caller's first connection; fetch the current roster before deriving an epoch.
        await ApplyGroupRosterAsync(attempt, await api.GetAsync<YapGroupCall>($"api/chat/calls/groups/{group.Id}", attempt.Lifetime.Token));
    }

    private YapCallInvite InviteFor(YapGroupCall group) => new(group.Id, group.ThreadId, group.CallerId,
        group.CallerName, chat.User!.CredentialId, group.ExpiresAt);

    private async Task ReceiveGroupAsync(YapCallEvent item)
    {
        var group = item.Group!;
        var attempt = active;
        try
        {
            var self = group.Participants.SingleOrDefault(x => x.CredentialId == chat.User!.CredentialId);
            if (self is null) return;
            if (item.Type == "group-ended" || self.Left)
            {
                RememberEnded(group.Id);
                if (attempt?.Group?.Id == group.Id) await EndAttemptAsync(attempt, false);
                return;
            }
            if (attempt is null && item.Type == "group-incoming" && !self.Left && !self.Accepted &&
                !endedInvites.Contains(group.Id) && group.ExpiresAt > DateTimeOffset.UtcNow)
            {
                active = new Attempt(chat.Scope) { Invite = InviteFor(group), Group = group };
                Name = group.CallerName; AvatarUrl = null; Incoming = true; Minimized = false;
                Status = "Incoming encrypted voice call"; Error = null; Notify();
                return;
            }
            if (attempt?.Group?.Id != group.Id || !Current(attempt)) return;
            await ApplyGroupRosterAsync(attempt, group);
            if (!Current(attempt) || item.Control is not { } control) return;
            if (!attempt.TransportReady || attempt.Epoch?.Revision != control.Revision)
            {
                if (control.Revision >= (attempt.Group?.Revision ?? 0) && attempt.PendingControls.Count < 16)
                    attempt.PendingControls[(control.SenderId, control.Kind)] = control;
                return;
            }
            await ReceiveEpochControlAsync(attempt, attempt.Epoch, control);
        }
        catch (Exception error) { if (attempt is not null) await FailAsync(attempt, error); }
    }

    private async Task ApplyGroupRosterAsync(Attempt attempt, YapGroupCall group)
    {
        if (!Current(attempt) || attempt.Group is { } prior && group.Revision < prior.Revision) return;
        var self = group.Participants.SingleOrDefault(x => x.CredentialId == chat.User!.CredentialId);
        if (self is null || self.Left) { await EndAttemptAsync(attempt, false); return; }
        attempt.Group = group; Notify();
        if (!self.Accepted || !attempt.TransportReady) return;
        if (self.DeviceId != ApprovedDevice()) throw new InvalidOperationException("This call belongs to another device.");
        if (attempt.Epoch?.Revision == group.Revision) return;
        await BeginEpochAsync(attempt, group);
    }

    private async Task BeginEpochAsync(Attempt attempt, YapGroupCall group)
    {
        var old = attempt.Epoch;
        var localId = chat.User!.CredentialId;
        var local = new SFrameSenderKey(MediaSender(group.Id, localId),
            BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8)).ToString(CultureInfo.InvariantCulture), RandomNumberGenerator.GetBytes(32));
        var epoch = attempt.Epoch = new GroupEpoch(group.Revision, ChatEncryption.CallRosterBinding(group), local,
            group.Participants.Where(x => x.Accepted && !x.Left && x.CredentialId != localId).Select(x => x.CredentialId).ToArray());
        attempt.Phase = "call-keys";
        Status = epoch.Peers.Length == 0 ? RingingStatus : "Securing call..."; Notify();
        // Close the managed send gate synchronously, before an older media operation can finish.
        var pause = attempt.Media!.PauseSFrameAsync();
        await RunEpochMediaAsync(attempt, epoch, () => pause);
        old?.ClearKeys();
        await LoadVideoLadderAsync(attempt);
        if (!CurrentEpoch(attempt, epoch)) return;
        if (epoch.Peers.Length == 0) return;
        _ = ExpireEpochAsync(attempt, epoch);
        foreach (var peer in epoch.Peers)
        {
            await SendEpochControlAsync(attempt, epoch, peer, "key",
                new CallKey(local.Kid, Convert.ToBase64String(local.Key), VideoCodecLadder.Advertise(attempt.Ladder?.Decodable ?? []), attempt.DecodeCeiling));
            if (!CurrentEpoch(attempt, epoch)) return;
        }
        var pending = attempt.PendingControls.Values.Where(x => x.Revision == epoch.Revision).ToArray();
        attempt.PendingControls.Clear();
        foreach (var control in pending) await ReceiveEpochControlAsync(attempt, epoch, control);
    }

    private bool CurrentEpoch(Attempt attempt, GroupEpoch epoch) => Current(attempt) && ReferenceEquals(attempt.Epoch, epoch) && attempt.Group?.Revision == epoch.Revision;
    private static string MediaSender(Guid call, Guid credential) => $"yap-media-{call:N}-{credential:N}";

    private async Task<bool> RunEpochMediaAsync(Attempt attempt, GroupEpoch epoch, Func<Task> action)
    {
        await attempt.MediaGate.WaitAsync(attempt.Lifetime.Token);
        try
        {
            if (!CurrentEpoch(attempt, epoch)) return false;
            await action();
            return CurrentEpoch(attempt, epoch);
        }
        finally { attempt.MediaGate.Release(); }
    }

    private async Task SendEpochControlAsync(Attempt attempt, GroupEpoch epoch, Guid recipient, string kind, CallKey payload)
    {
        if (!CurrentEpoch(attempt, epoch)) return;
        var sequence = ++epoch.Sequence;
        var envelope = await chat.Encryption.EncryptCallControlAsync(chat.User!, attempt.Group!, sequence, recipient, kind, payload);
        if (!CurrentEpoch(attempt, epoch)) return;
        try { await api.PostAsync($"api/chat/calls/groups/{attempt.Group!.Id}/control", new YapGroupControl(epoch.Revision, sequence, recipient, envelope, kind), attempt.Lifetime.Token); }
        catch (ChatApiException error) when (error.Status == 409)
        {
            if (Current(attempt)) await ApplyGroupRosterAsync(attempt,
                await api.GetAsync<YapGroupCall>($"api/chat/calls/groups/{attempt.Group!.Id}", attempt.Lifetime.Token));
        }
    }

    private async Task ReceiveEpochControlAsync(Attempt attempt, GroupEpoch epoch, YapGroupControlEvent control)
    {
        if (!CurrentEpoch(attempt, epoch) || control.Revision != epoch.Revision || !epoch.Peers.Contains(control.SenderId)) return;
        var slot = (control.SenderId, control.Kind);
        if (epoch.Seen.TryGetValue(slot, out var sequence) && control.Sequence <= sequence) return;
        var body = await chat.Encryption.DecryptCallControlAsync(chat.User!, attempt.Group!, control);
        if (!CurrentEpoch(attempt, epoch)) return;
        var payload = body.Deserialize<CallKey>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid call key.");
        if (!ulong.TryParse(payload.Kid, NumberStyles.None, CultureInfo.InvariantCulture, out _)) throw new InvalidOperationException("Invalid call key ID.");
        if (control.Kind == "key")
        {
            var key = Convert.FromBase64String(payload.Key ?? "");
            if (key.Length != 32 || payload.Kid == epoch.Local.Kid || epoch.Remote.Any(x => x.Key != control.SenderId && x.Value.Kid == payload.Kid))
                throw new InvalidOperationException("Invalid call key.");
            if (epoch.Remote.TryGetValue(control.SenderId, out var previous))
            {
                if (previous.Kid != payload.Kid || !CryptographicOperations.FixedTimeEquals(previous.Key, key))
                    throw new InvalidOperationException("A sender changed its key within an epoch.");
                CryptographicOperations.ZeroMemory(key);
            }
            else epoch.Remote[control.SenderId] = new(MediaSender(control.CallId, control.SenderId), payload.Kid, key);
            // Decoder advertisements are peer claims about their own hardware, nothing more: a bad
            // one can only cost that peer its picture, so a short unknown string is simply ignored.
            if (payload.Video is { Length: <= 32 } advertised)
                epoch.PeerCodecs[control.SenderId] = VideoCodecLadder.ReadAdvertisement(advertised);
            epoch.PeerVideoHeights[control.SenderId] = Math.Clamp(payload.VideoHeight, 240, 2160);
        }
        else if (control.Kind == "ack" && payload.Kid == epoch.Local.Kid && payload.Key is null) epoch.Acknowledged.Add(control.SenderId);
        else throw new InvalidOperationException("Invalid call acknowledgment.");
        epoch.Seen[slot] = control.Sequence;
        await CompleteEpochAsync(attempt, epoch);
    }

    private async Task CompleteEpochAsync(Attempt attempt, GroupEpoch epoch)
    {
        await epoch.Completion.WaitAsync(attempt.Lifetime.Token);
        try
        {
            if (!CurrentEpoch(attempt, epoch) || epoch.Remote.Count != epoch.Peers.Length || epoch.Peers.Length == 0) return;
            var media = attempt.Media!;
            if (!epoch.Installed)
            {
                if (!await RunEpochMediaAsync(attempt, epoch, () => media.InstallSFrameEpochAsync(epoch.Id, epoch.Binding, epoch.Local, epoch.Remote.Values.ToArray()))) return;
                epoch.Installed = true;
                foreach (var peer in epoch.Peers) await SendEpochControlAsync(attempt, epoch, peer, "ack", new CallKey(epoch.Remote[peer].Kid, null));
            }
            if (!CurrentEpoch(attempt, epoch) || epoch.Active || epoch.Acknowledged.Count != epoch.Peers.Length) return;
            if (!await RunEpochMediaAsync(attempt, epoch, () => media.ActivateSFrameEpochAsync(epoch.Id, epoch.Binding))) return;
            epoch.Active = true;
            try { await api.PostAsync($"api/chat/calls/groups/{attempt.Group!.Id}/ready", new YapGroupReady(epoch.Revision), attempt.Lifetime.Token); }
            catch (ChatApiException error) when (error.Status == 409)
            {
                if (Current(attempt)) await ApplyGroupRosterAsync(attempt,
                    await api.GetAsync<YapGroupCall>($"api/chat/calls/groups/{attempt.Group!.Id}", attempt.Lifetime.Token));
                return;
            }
            if (!CurrentEpoch(attempt, epoch)) return;
            if (!attempt.HostedAudioStarted)
            {
                if (!await RunEpochMediaAsync(attempt, epoch, () => media.StartHostedAudioAsync(attempt.Group.Id))) return;
                attempt.HostedAudioStarted = true;
            }
            if (!Muted && !await RunEpochMediaAsync(attempt, epoch, media.StartAudioAsync)) return;
            if (!CurrentEpoch(attempt, epoch)) return;
            ConnectedAt ??= DateTimeOffset.UtcNow; Status = "Connected"; Notify();
            NegotiateVideo(attempt, epoch);
            // A call started with the camera button opens it once, here, after the keys are live.
            // Detached on purpose: a camera permission prompt must not stall the call-event loop.
            if (attempt.WantsVideo && !attempt.CameraOn && !attempt.CameraBusy)
            { attempt.WantsVideo = false; _ = ToggleCameraAsync(); }
        }
        finally { epoch.Completion.Release(); }
    }

    private async Task ExpireEpochAsync(Attempt attempt, GroupEpoch epoch)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), attempt.Lifetime.Token);
            if (CurrentEpoch(attempt, epoch) && !epoch.Active) await FailAsync(attempt, new TimeoutException("Call key confirmation timed out."));
        }
        catch (OperationCanceledException) { }
    }

    private async Task ToggleGroupMuteAsync()
    {
        var attempt = active;
        if (attempt?.Group is null || attempt.Media is not { } media || !Current(attempt) || ConnectedAt is null || attempt.Muting) return;
        attempt.Muting = true;
        try
        {
            if (Muted && attempt.Epoch?.Active == true) await media.StartAudioAsync(); else await media.SetAudioMutedAsync(true);
            if (!Current(attempt)) { await media.StopAudioAsync(); return; }
            Muted = !Muted; Notify();
            await api.PostAsync($"api/chat/calls/groups/{attempt.Group.Id}/mute", new YapGroupMute(Muted), attempt.Lifetime.Token);
        }
        catch (Exception error) { await FailAsync(attempt, error); }
        finally { attempt.Muting = false; }
    }

    /// <summary>Epoch control payload. <c>Video</c> lists the codecs the sender can decode, so the
    /// choice of wire codec never leaves the end-to-end encrypted envelope.</summary>
    private sealed record CallKey(string Kid, string? Key, string? Video = null, int VideoHeight = 1080);

    /// <summary>Resolve the concurrent capability probe into a ladder, once per call.</summary>
    private async Task LoadVideoLadderAsync(Attempt attempt)
    {
        if (attempt.Ladder is not null || attempt.VideoProbe is not { } probe) return;
        try
        {
            await LoadVideoPreferenceAsync();
            var capabilities = await probe.WaitAsync(TimeSpan.FromSeconds(5), attempt.Lifetime.Token);
            if (!Current(attempt)) return;
            var ladder = new VideoCodecLadder();
            foreach (var codec in capabilities.Codecs)
                ladder.Record(new(VideoCodecLadder.Parse(codec.Codec), codec.Encode, codec.Decode, codec.Hardware, codec.MaxHeight));
            attempt.Ladder = ladder;
            attempt.Ceiling = capabilities.Ceiling;
            attempt.DecodeCeiling = capabilities.Codecs.Where(x => x.Decode).Select(x => x.DecodeMaxHeight).DefaultIfEmpty(0).Min();
            // Held apart from CodecNotice: negotiation recomputes that every epoch, and a missing
            // browser API is not something a later epoch can fix.
            attempt.VideoBlocked = capabilities.Supported ? null : capabilities.Reason;
            attempt.CodecNotice = attempt.VideoBlocked;
        }
        // A device without WebCodecs video still makes a perfectly good voice call.
        catch
        {
            attempt.Ladder = new VideoCodecLadder();
            attempt.CodecNotice = attempt.VideoBlocked =
                "This browser cannot send video in calls. Try the latest Safari, Chrome, Edge or Firefox.";
        }
    }

    private async Task InvokeRemoteVideoAsync(Attempt attempt)
    { await Task.Yield(); ApplyRemoteVideo(attempt); }
    private sealed class GroupEpoch(long revision, string binding, SFrameSenderKey local, Guid[] peers)
    {
        public long Revision { get; } = revision;
        public string Id => Revision.ToString(CultureInfo.InvariantCulture);
        public string Binding { get; } = binding;
        public SFrameSenderKey Local { get; } = local;
        public Guid[] Peers { get; } = peers;
        public Dictionary<Guid, SFrameSenderKey> Remote { get; } = [];
        public Dictionary<Guid, VideoCodec[]> PeerCodecs { get; } = [];
        public Dictionary<Guid, int> PeerVideoHeights { get; } = [];
        public HashSet<Guid> Acknowledged { get; } = [];
        public Dictionary<(Guid, string), long> Seen { get; } = [];
        public SemaphoreSlim Completion { get; } = new(1, 1);
        public long Sequence;
        public bool Installed, Active;
        public void ClearKeys() { CryptographicOperations.ZeroMemory(Local.Key); foreach (var key in Remote.Values) CryptographicOperations.ZeroMemory(key.Key); }
    }
}
