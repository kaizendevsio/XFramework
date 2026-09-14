using Bolt.Client;
using Bolt.Media.Browser;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class VoiceState : IAsyncDisposable
{
    private readonly ChatState chat;
    private readonly ChatApi api;
    private readonly IServiceScopeFactory scopes;
    private readonly NavigationManager navigation;
    private readonly ILoggerFactory logs;
    private Attempt? active;
    private readonly Queue<Guid> endedInvites = new();
    private string configured = "";
    private bool disposed;
    public bool Enabled { get; private set; }
    public bool IsCalling => active is not null;
    public YapCallInvite? Invite => active?.Invite;
    public string Name { get; private set; } = "";
    public string? AvatarUrl { get; private set; }
    public string Status { get; private set; } = "";
    public string? Error { get; private set; }
    public bool Incoming { get; private set; }
    public bool Muted { get; private set; }
    public bool Minimized { get; set; }
    public DateTimeOffset? ConnectedAt { get; private set; }
    public event Action? Changed;

    public VoiceState(ChatState chat, ChatApi api, IServiceScopeFactory scopes, NavigationManager navigation, ILoggerFactory logs)
    {
        this.chat = chat; this.api = api; this.scopes = scopes; this.navigation = navigation; this.logs = logs;
        chat.CallReceived += ReceiveAsync;
        chat.Changed += AccountChanged;
    }

    private bool Current(Attempt attempt) => !disposed && ReferenceEquals(active, attempt) && !attempt.Ended &&
        !attempt.Lifetime.IsCancellationRequested && chat.User is not null && !chat.NeedsLogin &&
        chat.Scope == attempt.Account && api.Account == attempt.Account;
    private void CheckCurrent(Attempt attempt)
    { if (!Current(attempt)) throw new OperationCanceledException(attempt.Lifetime.Token); }
    private void AccountChanged()
    {
        if (active is { } attempt && !Current(attempt)) _ = EndAttemptAsync(attempt, false);
        if (chat.User is null || chat.NeedsLogin || api.Account != chat.Scope)
        { Enabled = false; configured = ""; Changed?.Invoke(); }
    }

    public async Task InitializeAsync()
    {
        if (active is { } attempt && !Current(attempt)) await EndAttemptAsync(attempt, false);
        if (disposed || chat.User is null || chat.NeedsLogin || api.Account != chat.Scope)
        { Enabled = false; configured = ""; return; }
        var account = chat.Scope;
        if (configured == account) return;
        configured = account;
        try
        {
            var configuration = await api.GetAsync<Configuration>("api/chat/calls/config");
            if (!disposed && chat.Scope == account && api.Account == account && !chat.NeedsLogin)
            { Enabled = configuration.Enabled && configuration.GroupCalls && configuration.SecurityMode == "EndToEndEncrypted"; Changed?.Invoke(); }
        }
        catch { if (configured == account) configured = ""; }
    }

    private async Task PrepareAsync(Attempt attempt)
    {
        CheckCurrent(attempt);
        attempt.Scope = scopes.CreateAsyncScope();
        var media = attempt.Media = attempt.Scope.Value.ServiceProvider.GetRequiredService<BoltMediaService>();
        var capability = await media.CheckVoiceCapabilitiesAsync();
        CheckCurrent(attempt);
        if (!capability.Supported) throw new InvalidOperationException(capability.Reason);
        await media.PrepareVoiceAsync();
        CheckCurrent(attempt);
    }

    public Task StartAsync(Guid thread, Person person)
    {
        return StartGroupAsync(thread, person.Name, [person]);
    }

    public Task AcceptAsync()
    {
        var attempt = active;
        if (attempt is null || !Incoming || attempt.Starting || !Current(attempt)) return Task.CompletedTask;
        attempt.Starting = true; Error = null; Status = "Connecting..."; Incoming = false; Changed?.Invoke();
        return attempt.Setup = AcceptGroupCoreAsync(attempt);
    }
    private async Task ReceiveAsync(YapCallEvent item)
    {
        if (disposed || chat.User is null || chat.NeedsLogin || api.Account != chat.Scope) return;
        if (item.Group is not null) { await ReceiveGroupAsync(item); return; }
        // Unencrypted invitations never enter the encrypted calling flow.
    }

    public async Task ToggleMuteAsync()
    {
        await ToggleGroupMuteAsync();
    }

    public async Task<AudioOutputs> GetAudioOutputsAsync() => active?.Media is { } media
        ? await media.GetAudioOutputsAsync() : new(false, "", []);
    public async Task<string> GetPlaybackStateAsync() => active?.Media is { } media
        ? await media.GetPlaybackStateAsync() : "closed";
    public async Task<bool> ResumePlaybackAsync() => active?.Media is { } media && await media.ResumePlaybackAsync();

    public async Task<AudioOutputs> SetAudioOutputAsync(string deviceId) => active?.Media is { } media
        ? await media.SetAudioOutputAsync(deviceId) : new(false, "", []);

    private Task FailAsync(Attempt attempt, Exception error)
    {
        if (!Current(attempt)) return Task.CompletedTask;
        var reason = Regex.Replace(error.Message, @"(?:https?|wss?)://\S+|(?i:ticket)=[^&\s]+", "[redacted endpoint]");
        logs.CreateLogger("Yap.Voice").LogWarning("Voice call failed ({ErrorType}): {Reason}", error.GetType().Name,
            attempt.Group is null ? reason : "Encrypted call could not complete");
        Error = error.Message switch
        {
            "Open Yap using its HTTPS address to use voice calls." => error.Message,
            "Voice calls are not supported by this browser. Try an updated browser." => error.Message,
            "Voice calls require a secure connection to Yap." => error.Message,
            "Approve this device in Settings before calling." => error.Message,
            _ => "The call could not connect. Check your connection and microphone access."
        };
        return EndAttemptAsync(attempt, true);
    }
    public Task EndAsync(bool notify = true) => active is { } attempt ? EndAttemptAsync(attempt, notify) : Task.CompletedTask;
    private async Task EndAttemptAsync(Attempt attempt, bool notify)
    {
        if (attempt.Ended) return;
        attempt.Ended = true;
        if (attempt.Invite is { } ended) RememberEnded(ended.Id);
        attempt.Lifetime.Cancel();
        if (ReferenceEquals(active, attempt))
        {
            active = null; Incoming = false; ConnectedAt = null; Minimized = false; Muted = false; Changed?.Invoke();
        }
        // Stop capture before network notification, including a pending microphone permission request.
        if (attempt.Media is { } media)
        {
            await attempt.MediaGate.WaitAsync();
            try { try { await media.StopAudioAsync(); } catch { } try { await media.CancelPreparedVoiceAsync(); } catch { } }
            finally { attempt.MediaGate.Release(); }
        }
        attempt.Epoch?.ClearKeys();
        if (notify) _ = NotifyEndAsync(attempt);
        if (attempt.Client is { } client)
        {
            client.Disconnected -= attempt.Disconnected;
            try { await client.DisposeAsync(); } catch { }
        }
        // Preparation can still be awaiting a browser permission prompt. Its continuation is fenced
        // by Current(), then this task disposes that attempt's scope without touching a newer call.
        _ = DisposeAttemptAsync(attempt, notify);
    }
    private async Task DisposeAttemptAsync(Attempt attempt, bool notify)
    {
        try
        {
            try { await attempt.Setup; } catch { }
            if (attempt.Scope is { } owned) { try { await owned.DisposeAsync(); } catch { } }
            if (notify) await NotifyEndAsync(attempt);
        }
        finally { attempt.Lifetime.Dispose(); }
    }
    private async Task NotifyEndAsync(Attempt attempt)
    {
        if (attempt.Notified || attempt.Invite is not { } invite || api.Account != attempt.Account) return;
        RememberEnded(invite.Id);
        attempt.Notified = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await api.PostAsync($"api/chat/calls/groups/{invite.Id}/leave", new { }, timeout.Token); } catch { }
    }
    private async Task EndAfterCallbackAsync(Attempt attempt)
    { await Task.Yield(); await EndAttemptAsync(attempt, false); }
    private void RememberEnded(Guid id)
    {
        if (endedInvites.Contains(id)) return;
        if (endedInvites.Count == 32) endedInvites.Dequeue();
        endedInvites.Enqueue(id);
    }
    public void DismissError() { Error = null; Changed?.Invoke(); }
    public async ValueTask DisposeAsync()
    { disposed = true; chat.CallReceived -= ReceiveAsync; chat.Changed -= AccountChanged; await EndAsync(); }
    private sealed record Configuration(bool Enabled, bool GroupCalls = false, string SecurityMode = "");
    private sealed class Attempt(string account)
    {
        public string Account { get; } = account;
        public CancellationTokenSource Lifetime { get; } = new();
        public AsyncServiceScope? Scope;
        public BoltMediaService? Media;
        public BoltClient? Client;
        public YapCallInvite? Invite;
        public YapCallConnection? Connection;
        public Action? Disconnected;
        public Task Setup = Task.CompletedTask;
        public YapGroupCall? Group;
        public GroupEpoch? Epoch;
        public bool TransportReady, HostedAudioStarted;
        public SemaphoreSlim MediaGate { get; } = new(1, 1);
        public Dictionary<(Guid, string), YapGroupControlEvent> PendingControls { get; } = [];
        public bool Starting, Ended, Muting, Notified;
    }
}
