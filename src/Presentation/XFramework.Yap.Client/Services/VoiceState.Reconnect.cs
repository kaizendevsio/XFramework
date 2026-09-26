using Bolt.Client;
using Bolt.Media;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Yap.Contracts;

namespace Yap.Client.Services;

/// <summary>What the call screen shows after a call ended by itself: why, and who to call back.</summary>
public sealed record CallEndedNotice(string Title, string Detail, Guid ThreadId, string Name, string? AvatarUrl, bool Video, bool Redialing = false);

/// <summary>
/// Resumable calls, client side.
///
/// Connected → Degraded ("Poor connection": the relay has gone quiet for a while, or answers slowly)
/// → Reconnecting (the transport is gone) → Connected again, or Failed once the seat's grace period
/// runs out or the server refuses the resume. <see cref="CallLinkMonitor"/> decides from heartbeat
/// echoes and inbound frames, with thresholds scaled by the round trip; <see cref="CallReconnector"/>
/// paces the attempts and restarts at once on a network hint (online, network change, page shown).
///
/// A resume swaps only the transport. The microphone and camera stay open (the page-hide rule still
/// releases the camera), the SFrame session and its epoch continue, so no key is renegotiated and no
/// nonce is reused, and mute and camera state carry over. Only if the roster changed while this
/// device was away does it join the new epoch the normal way.
/// </summary>
public sealed partial class VoiceState
{
    private int reconnectGraceSeconds = 45;
    private DotNetObjectReference<VoiceState>? networkReference;

    /// <summary>The call's connection, as the call screen should describe it.</summary>
    public CallLinkState Link => active is { } attempt && attempt.EverConnected ? attempt.Link.State : CallLinkState.Connected;
    public bool PoorConnection => Link == CallLinkState.Degraded;
    public bool Reconnecting => Link == CallLinkState.Reconnecting;
    /// <summary>When this device lost its connection, while reconnecting.</summary>
    public DateTimeOffset? ReconnectingSince => Reconnecting ? active?.ReconnectingSince : null;
    /// <summary>When a reconnect gives up if it has not succeeded by then.</summary>
    public DateTimeOffset? ReconnectDeadline => ReconnectingSince?.AddSeconds(reconnectGraceSeconds);
    /// <summary>Set after a call ended because a connection was lost; the screen stays up to say so and offer a call back.</summary>
    public CallEndedNotice? Ended { get; private set; }
    /// <summary>The server is holding this participant's seat while they reconnect.</summary>
    public bool IsAway(Guid credential) => active?.Group?.Participants.Any(x => x.CredentialId == credential && x.Reconnecting && !x.Left) == true;

    private static long LinkNow() => Environment.TickCount64;
    private CallLinkOptions LinkOptions() => new() { GraceMs = reconnectGraceSeconds * 1000 };

    // ── Losing the transport ──

    /// <summary><see cref="BoltClient.Disconnected"/> for <paramref name="client"/>. A replaced client's late event is ignored.</summary>
    private void TransportLost(Attempt attempt, BoltClient client)
    {
        if (!Current(attempt)) return;
        if (!attempt.EverConnected) { _ = EndAfterCallbackAsync(attempt); return; }
        if (!ReferenceEquals(attempt.Client, client)) return;
        _ = ReconnectAfterCallbackAsync(attempt);
    }

    private async Task ReconnectAfterCallbackAsync(Attempt attempt)
    { await Task.Yield(); await BeginReconnectAsync(attempt); }

    /// <param name="noticed">The monitor itself declared the link dead (and is already reconnecting).</param>
    private async Task BeginReconnectAsync(Attempt attempt, bool noticed = false)
    {
        if (!Current(attempt)) return;
        if (!noticed && !attempt.Link.Lost(LinkNow()))
        {
            // Already reconnecting. If the connection that just died is the one a resume produced,
            // the loop that produced it must go round again once it returns.
            attempt.LostDuringResume = true;
            attempt.Reconnector?.Nudge();
            return;
        }
        attempt.TransportReady = false;
        attempt.LostDuringResume = false;
        attempt.ReconnectingSince = DateTimeOffset.UtcNow;
        Status = "Reconnecting..."; Notify();
        _ = RecordLinkAsync("call.reconnecting", attempt);
        logs.CreateLogger("Yap.Voice").LogInformation("Call transport lost; resuming (rtt {Rtt} ms)", attempt.Link.SmoothedRttMs);
        await DetachTransportAsync(attempt);
        if (!Current(attempt)) return;
        RefreshFrozenTiles(attempt);
        var reconnector = attempt.Reconnector = new CallReconnector(attempt.Link, LinkNow, static (delay, ct) => Task.Delay(delay, ct));
        attempt.Reconnect = ReconnectLoopAsync(attempt, reconnector);
    }

    /// <summary>Let go of the current client; the media service keeps the call itself.</summary>
    private async Task DetachTransportAsync(Attempt attempt)
    {
        var client = attempt.Client;
        attempt.Client = null;
        if (client is not null)
        {
            if (attempt.Disconnected is { } handler) client.Disconnected -= handler;
            _ = DisposeQuietlyAsync(client);
        }
        if (attempt.Media is not { HasTransport: true } media) return;
        await attempt.MediaGate.WaitAsync();
        try { await media.SuspendTransportAsync(); }
        catch (Exception error) { logs.CreateLogger("Yap.Voice").LogDebug(error, "Detaching the call transport failed"); }
        finally { attempt.MediaGate.Release(); }
    }

    private static async Task DisposeQuietlyAsync(BoltClient client)
    {
        try { await client.DisposeAsync(); } catch { /* The connection is already gone. */ }
    }

    private async Task ReconnectLoopAsync(Attempt attempt, CallReconnector reconnector)
    {
        var outcome = await reconnector.RunAsync(ct => ResumeOnceAsync(attempt, ct), attempt.Lifetime.Token);
        if (!Current(attempt)) return;
        switch (outcome)
        {
            case CallResumeOutcome.Resumed:
                attempt.ReconnectingSince = null;
                Status = attempt.Epoch?.Active == true ? "Connected" : "Securing call...";
                _ = RecordLinkAsync("call.resumed", attempt, reconnector.Attempts);
                logs.CreateLogger("Yap.Voice").LogInformation("Call resumed after {Attempts} attempt(s)", reconnector.Attempts);
                Notify();
                if (attempt.LostDuringResume) { attempt.LostDuringResume = false; await BeginReconnectAsync(attempt); }
                break;
            case CallResumeOutcome.GaveUp or CallResumeOutcome.Refused:
                _ = RecordLinkAsync("call.connection-lost", attempt, reconnector.Attempts);
                ShowConnectionLost(attempt);
                await EndAttemptAsync(attempt, true);
                break;
        }
    }

    /// <summary>
    /// One resume: a fresh ticket for this seat, a new socket (a new TCP connection, so a changed
    /// network or address is simply the new route), the relay's room again, then the local streams.
    /// </summary>
    private async Task<CallResumeAttempt> ResumeOnceAsync(Attempt attempt, CancellationToken ct)
    {
        if (!Current(attempt) || attempt.Group is not { } group || attempt.Media is not { } media) return CallResumeAttempt.Refused;
        BoltClient? client = null;
        Action? handler = null;
        try
        {
            var connection = await api.PostAsync<YapCallConnection>($"api/chat/calls/groups/{group.Id}/resume",
                new YapGroupResume(ApprovedDevice()), ct) ?? throw new InvalidOperationException("The call connection was not supplied.");
            CheckCurrent(attempt);
            if (connection.ClientId != MediaSender(group.Id, chat.User!.CredentialId)) return CallResumeAttempt.Refused;
            var endpoint = navigation.ToAbsoluteUri(connection.Url);
            var origin = navigation.ToAbsoluteUri("/");
            if (origin.Scheme != "https" || endpoint.Scheme != "https" || endpoint.Authority != origin.Authority) return CallResumeAttempt.Refused;
            client = new BoltClient(new UriBuilder(endpoint) { Scheme = "wss" }.Uri, connection.ClientId, "Yap encrypted voice",
                new BoltClientOptions { MinConnections = 1, MaxConnections = 1, MaxFrameBytes = 65536, AutoReconnect = false },
                logs.CreateLogger("Yap.Voice"));
            var resumed = client;
            handler = () => TransportLost(attempt, resumed);
            client.Disconnected += handler;
            await attempt.MediaGate.WaitAsync(ct);
            try { media.AttachTransport(client); }
            finally { attempt.MediaGate.Release(); }
            await client.ConnectAsync(ct);
            CheckCurrent(attempt);
            await media.JoinHostedGroupAsync(group.Id);
            attempt.Client = client; attempt.Disconnected = handler; attempt.Connection = connection;

            var roster = await api.GetAsync<YapGroupCall>($"api/chat/calls/groups/{group.Id}", ct);
            CheckCurrent(attempt);
            var self = roster.Participants.SingleOrDefault(x => x.CredentialId == chat.User!.CredentialId);
            if (self is null || self.Left) return CallResumeAttempt.Refused;
            attempt.TransportReady = true;
            if (attempt.Epoch is { } epoch && roster.Revision == epoch.Revision)
            {
                attempt.Group = roster;
                if (epoch.Active && attempt.HostedAudioStarted)
                {
                    // Same roster, same epoch: rejoin the relay's room and publish again. No key changes hands.
                    await api.PostAsync($"api/chat/calls/groups/{group.Id}/ready", new YapGroupReady(epoch.Revision), ct);
                    CheckCurrent(attempt);
                    if (!await RunEpochMediaAsync(attempt, epoch, () => media.StartHostedAudioAsync(group.Id))) return CallResumeAttempt.Retry;
                    if (attempt.CameraOn) await RunEpochMediaAsync(attempt, epoch, () => media.ResumeVideoStreamAsync(group.Id));
                }
                else
                {
                    // The transport went while this epoch was still being set up. Its keys stand (a sender
                    // never changes key within an epoch); finish it the normal way now there is a socket.
                    attempt.ResumeVideo = attempt.CameraOn;
                    epoch.Active = false;
                    _ = FinishEpochAsync(attempt, epoch);
                }
            }
            else
            {
                // The roster moved on while this device was away (or the epoch never finished): join the
                // current epoch properly. It completes as the keys arrive, on its own.
                attempt.HostedAudioStarted = false;
                attempt.ResumeVideo = attempt.CameraOn;
                _ = RejoinEpochAsync(attempt, roster);
            }
            if (attempt.MuteDirty) _ = SyncMuteAsync(attempt);
            RefreshFrozenTiles(attempt);
            return CallResumeAttempt.Resumed;
        }
        catch (ChatApiException error) when (error.Status is 403 or 404 or 410 && Current(attempt))
        {
            await AbandonAsync(attempt, client, handler);
            return CallResumeAttempt.Refused;
        }
        catch (ChatApiException error) when (error.Status == 409 && Current(attempt) && client is not null && attempt.TransportReady)
        {
            // The roster changed between reading it and rejoining: the socket is fine, the epoch is not.
            attempt.HostedAudioStarted = false;
            attempt.ResumeVideo = attempt.CameraOn;
            _ = RejoinEpochAsync(attempt, null);
            return CallResumeAttempt.Resumed;
        }
        catch (Exception) when (Current(attempt))
        {
            await AbandonAsync(attempt, client, handler);
            return CallResumeAttempt.Retry;
        }
    }

    private async Task RejoinEpochAsync(Attempt attempt, YapGroupCall? roster)
    {
        try
        {
            roster ??= await api.GetAsync<YapGroupCall>($"api/chat/calls/groups/{attempt.Group!.Id}", attempt.Lifetime.Token);
            if (attempt.Epoch is { } epoch && epoch.Revision == roster.Revision) await FinishEpochAsync(attempt, epoch);
            else await ApplyGroupRosterAsync(attempt, roster);
        }
        catch (OperationCanceledException) when (!Current(attempt)) { }
        catch (Exception error) { await FailAsync(attempt, error); }
    }

    /// <summary>Process key controls that arrived while there was no transport, then complete the epoch.</summary>
    private async Task FinishEpochAsync(Attempt attempt, GroupEpoch epoch)
    {
        try
        {
            var pending = attempt.PendingControls.Values.Where(x => x.Revision == epoch.Revision).ToArray();
            foreach (var control in pending) attempt.PendingControls.Remove((control.SenderId, control.Kind));
            foreach (var control in pending) await ReceiveEpochControlAsync(attempt, epoch, control);
            await CompleteEpochAsync(attempt, epoch);
        }
        catch (OperationCanceledException) when (!Current(attempt)) { }
        catch (Exception error) { await FailAsync(attempt, error); }
    }

    /// <summary>Undo a resume attempt that did not make it, so the next one starts from a clean slate.</summary>
    private async Task AbandonAsync(Attempt attempt, BoltClient? client, Action? handler)
    {
        attempt.TransportReady = false;
        if (client is null) return;
        if (handler is not null) client.Disconnected -= handler;
        if (ReferenceEquals(attempt.Client, client)) { attempt.Client = null; attempt.Disconnected = null; }
        if (attempt.Media is { HasTransport: true } media)
        {
            await attempt.MediaGate.WaitAsync();
            try { await media.SuspendTransportAsync(); }
            catch { /* Already detached. */ }
            finally { attempt.MediaGate.Release(); }
        }
        await DisposeQuietlyAsync(client);
    }

    private async Task SyncMuteAsync(Attempt attempt)
    {
        attempt.MuteDirty = false;
        try { await api.PostAsync($"api/chat/calls/groups/{attempt.Group!.Id}/mute", new YapGroupMute(Muted), attempt.Lifetime.Token); }
        catch (Exception) when (Current(attempt)) { attempt.MuteDirty = true; }
        catch (Exception) { /* The call ended. */ }
    }

    // ── Watching the link ──

    /// <summary>
    /// Once a second: fold in what arrived, send a heartbeat every <see cref="CallLinkOptions.HeartbeatIntervalMs"/>,
    /// and let <see cref="CallLinkMonitor"/> say whether the call is healthy, poor, or needs a new transport.
    /// </summary>
    private async Task LivenessAsync(Attempt attempt)
    {
        var link = attempt.Link;
        long lastHeartbeat = long.MinValue / 2, lastTick = LinkNow();
        var shown = link.State;
        try
        {
            while (Current(attempt))
            {
                await Task.Delay(1000, attempt.Lifetime.Token);
                if (!Current(attempt)) return;
                RefreshFrozenTiles(attempt);
                if (link.State is CallLinkState.Reconnecting or CallLinkState.Failed || !attempt.TransportReady ||
                    attempt.Media is not { } media || attempt.Group is not { } group)
                { shown = link.State; continue; }
                var now = LinkNow();
                // A tick that comes seconds late means the page was suspended (a backgrounded iOS PWA).
                // Frames the browser buffered meanwhile may not have been handed over yet, so silence
                // proves nothing: give the socket the short probe window instead.
                if (now - lastTick > 3_000) { link.Inbound(now); link.Probe(now); }
                lastTick = now;
                if (media.LastInboundTick is { } inbound) link.Inbound(inbound);
                if (now - lastHeartbeat >= link.Options.HeartbeatIntervalMs)
                {
                    lastHeartbeat = now;
                    _ = media.SendHeartbeatAsync(group.Id, now);
                }
                var state = link.Evaluate(now);
                if (state == CallLinkState.Reconnecting) { await BeginReconnectAsync(attempt, noticed: true); shown = state; continue; }
                if (state != shown) { shown = state; Notify(); }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// Online, a network type change, or the page coming back: while connected, give the socket a short
    /// chance to prove it survived; while reconnecting, try now instead of after the backoff.
    /// </summary>
    [JSInvokable]
    public Task OnCallNetworkChanged(string kind)
    {
        if (active is not { EverConnected: true } attempt || !Current(attempt)) return Task.CompletedTask;
        if (attempt.Link.State == CallLinkState.Reconnecting) { attempt.Reconnector?.Nudge(); return Task.CompletedTask; }
        var now = LinkNow();
        // Back from the background, the last frame is old for a reason that says nothing about the socket.
        if (kind == "visible") attempt.Link.Inbound(now);
        attempt.Link.Probe(now);
        if (attempt.Media is { } media && attempt.Group is { } group) _ = media.SendHeartbeatAsync(group.Id, now);
        return Task.CompletedTask;
    }

    private async Task WatchNetworkAsync(Attempt attempt)
    {
        try
        {
            networkReference ??= DotNetObjectReference.Create(this);
            var module = await js.InvokeAsync<IJSObjectReference>("import", "./call-network.js");
            var watch = await module.InvokeAsync<IJSObjectReference>("watch", networkReference);
            if (Current(attempt)) attempt.NetworkWatch = watch;
            else await watch.InvokeVoidAsync("dispose");
        }
        catch (Exception) { /* Hints only speed a reconnect up; the heartbeat still finds a dead link. */ }
    }

    private static async Task StopWatchingNetworkAsync(Attempt attempt)
    {
        if (attempt.NetworkWatch is not { } watch) return;
        attempt.NetworkWatch = null;
        try { await watch.InvokeVoidAsync("dispose"); await watch.DisposeAsync(); }
        catch (Exception) { /* The page is going away. */ }
    }

    private async Task RecordLinkAsync(string kind, Attempt attempt, int attempts = 0)
    {
        try
        {
            // The recorder keeps only whitelisted numbers: ms is how long the call was away, count the attempts.
            await js.InvokeVoidAsync("yap.diagnostics.record", kind, new
            {
                ms = attempt.ReconnectingSince is { } since ? (int)(DateTimeOffset.UtcNow - since).TotalMilliseconds : 0,
                count = attempts
            });
        }
        catch { /* Diagnostics never hold up a call. */ }
    }

    // ── The end, and calling back ──

    private void ShowConnectionLost(Attempt attempt)
    {
        if (attempt.Group is not { } group) return;
        Ended = new CallEndedNotice("Call ended", "connection lost", group.ThreadId, Name, AvatarUrl, attempt.CameraOn || RemoteVideo.Count != 0);
        Minimized = false;
    }

    /// <summary>"Call back" from the ended screen: the same conversation, the same kind of call, in the same dialog.</summary>
    public async Task CallBackAsync()
    {
        if (Ended is not { Redialing: false } ended || active is not null) return;
        Ended = ended with { Redialing = true }; Notify();
        try
        {
            await InitializeAsync();
            var conversation = await chat.ConversationDetailsAsync(ended.ThreadId);
            await StartGroupAsync(conversation.Id, conversation.Name, conversation.People, ended.Video && VideoAvailable);
        }
        catch (Exception error) { chat.Report(error); }
        finally
        {
            // Nothing started (offline, or calls are off): stay on the ended screen, ready to try again.
            if (active is null && Ended is { Redialing: true } still) { Ended = still with { Redialing = false }; Notify(); }
        }
    }

    public void DismissEnded() { Ended = null; Notify(); }
}
