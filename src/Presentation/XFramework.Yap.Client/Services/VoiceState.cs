using Bolt.Client;
using Bolt.Media.Browser;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed class VoiceState : IAsyncDisposable
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
            { Enabled = configuration.Enabled; Changed?.Invoke(); }
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
        if (!Enabled || disposed || active is not null || chat.User is null || chat.NeedsLogin || api.Account != chat.Scope)
            return Task.CompletedTask;
        var attempt = active = new Attempt(chat.Scope) { Starting = true };
        Error = null; Name = person.Name; AvatarUrl = person.AvatarUrl; Status = "Preparing microphone...";
        Incoming = false; Minimized = false; Changed?.Invoke();
        return attempt.Setup = StartCoreAsync(attempt, thread, person);
    }

    private async Task StartCoreAsync(Attempt attempt, Guid thread, Person person)
    {
        try
        {
            await PrepareAsync(attempt);
            attempt.Invite = await api.PostAsync<YapCallInvite>("api/chat/calls/", new StartYapCall(thread, person.Id), attempt.Lifetime.Token);
            CheckCurrent(attempt);
            Status = "Calling..."; Changed?.Invoke();
            await ConnectAsync(attempt);
            if (Current(attempt) && ConnectedAt is null) { Status = "Ringing..."; Changed?.Invoke(); }
        }
        catch (OperationCanceledException) when (!Current(attempt)) { }
        catch (Exception ex) { await FailAsync(attempt, ex); }
        finally { attempt.Starting = false; if (Current(attempt)) Changed?.Invoke(); }
    }

    public Task AcceptAsync()
    {
        var attempt = active;
        if (attempt is null || !Incoming || attempt.Starting || !Current(attempt)) return Task.CompletedTask;
        attempt.Starting = true; Error = null; Status = "Connecting..."; Incoming = false; Changed?.Invoke();
        return attempt.Setup = AcceptCoreAsync(attempt);
    }
    private async Task AcceptCoreAsync(Attempt attempt)
    {
        try { await PrepareAsync(attempt); await ConnectAsync(attempt); }
        catch (OperationCanceledException) when (!Current(attempt)) { }
        catch (Exception ex) { await FailAsync(attempt, ex); }
        finally { attempt.Starting = false; if (Current(attempt)) Changed?.Invoke(); }
    }

    private async Task ConnectAsync(Attempt attempt)
    {
        CheckCurrent(attempt);
        var invite = attempt.Invite ?? throw new InvalidOperationException("The call has ended.");
        var connection = await api.PostAsync<YapCallConnection>($"api/chat/calls/{invite.Id}/connect", ct: attempt.Lifetime.Token)
            ?? throw new InvalidOperationException("The call could not connect.");
        CheckCurrent(attempt);
        attempt.Connection = connection;
        var endpoint = navigation.ToAbsoluteUri(connection.Url);
        var origin = navigation.ToAbsoluteUri("/");
        if (origin.Scheme != "https" || endpoint.Scheme != "https" || endpoint.Authority != origin.Authority)
            throw new InvalidOperationException("Voice calls require a secure connection to Yap.");
        var uri = new UriBuilder(endpoint) { Scheme = "wss" };
        var client = attempt.Client = new BoltClient(uri.Uri, connection.ClientId, "Yap voice",
            new BoltClientOptions { MinConnections = 1, MaxConnections = 1, MaxFrameBytes = 262144 }, logs.CreateLogger("Yap.Voice"));
        var media = attempt.Media!;
        media.OnIncomingCall += async info =>
        {
            if (!Current(attempt) || invite.Id != info.CallId || invite.CallerId == chat.User?.CredentialId) return;
            try { await media.AnswerCallAsync(info.CallId); }
            catch (Exception ex) { await FailAsync(attempt, ex); }
        };
        media.OnCallAnswered += async id =>
        {
            if (!Current(attempt) || invite.Id != id) return;
            try
            {
                await media.StartAudioAsync();
                if (!Current(attempt)) { await media.StopAudioAsync(); return; }
                ConnectedAt = DateTimeOffset.UtcNow; Status = "Connected"; Changed?.Invoke();
            }
            catch (Exception ex) { await FailAsync(attempt, ex); }
        };
        media.OnCallEnded += id => { if (id == invite.Id) _ = EndAfterCallbackAsync(attempt); return Task.CompletedTask; };
        media.OnCallRejected += (id, reason) => { if (id == invite.Id) _ = EndAfterCallbackAsync(attempt); return Task.CompletedTask; };
        attempt.Disconnected = () => { if (Current(attempt)) _ = EndAttemptAsync(attempt, false); };
        client.Disconnected += attempt.Disconnected;
        await media.InitializeAsync(client);
        CheckCurrent(attempt);
        await client.ConnectAsync(attempt.Lifetime.Token);
        CheckCurrent(attempt);
        await api.PostAsync($"api/chat/calls/{invite.Id}/ready", new { }, attempt.Lifetime.Token);
        CheckCurrent(attempt);
        attempt.LocalReady = true;
        await InitiateAsync(attempt);
    }

    private async Task InitiateAsync(Attempt attempt)
    {
        var invite = attempt.Invite;
        if (!Current(attempt) || attempt.Initiated || !attempt.PeerReady || !attempt.LocalReady ||
            attempt.Connection is null || invite is null || invite.CallerId != chat.User?.CredentialId) return;
        attempt.Initiated = true;
        await attempt.Media!.StartCallAsync(attempt.Connection.RecipientClientId, callId: invite.Id);
    }

    private async Task ReceiveAsync(YapCallEvent item)
    {
        if (disposed || chat.User is null || chat.NeedsLogin || api.Account != chat.Scope) return;
        var attempt = active;
        try
        {
            if (item.Type == "incoming" && item.Invite.RecipientId == chat.User.CredentialId && attempt is null &&
                !endedInvites.Contains(item.Invite.Id) && item.Invite.ExpiresAt > DateTimeOffset.UtcNow)
            {
                active = new Attempt(chat.Scope) { Invite = item.Invite };
                Name = item.Invite.CallerName; AvatarUrl = null; Incoming = true; Minimized = false;
                Status = "Incoming voice call"; Error = null; Changed?.Invoke();
            }
            else if (attempt?.Invite?.Id == item.Invite.Id && item.Type == "ready" && item.CredentialId == attempt.Invite.RecipientId)
            { attempt.PeerReady = true; await InitiateAsync(attempt); }
            else if (attempt?.Invite?.Id == item.Invite.Id && item.Type == "ended") await EndAttemptAsync(attempt, false);
        }
        catch (Exception ex) { if (attempt is not null) await FailAsync(attempt, ex); }
    }

    public async Task ToggleMuteAsync()
    {
        var attempt = active;
        if (attempt?.Media is not { } media || !Current(attempt) || ConnectedAt is null || attempt.Muting) return;
        attempt.Muting = true;
        try
        {
            if (Muted) await media.StartAudioAsync(); else await media.StopAudioAsync();
            if (Current(attempt)) { Muted = !Muted; Changed?.Invoke(); }
            else await media.StopAudioAsync();
        }
        catch (Exception ex) { await FailAsync(attempt, ex); }
        finally { attempt.Muting = false; }
    }

    private Task FailAsync(Attempt attempt, Exception error)
    {
        if (!Current(attempt)) return Task.CompletedTask;
        var reason = Regex.Replace(error.Message, @"(?:https?|wss?)://\S+|(?i:ticket)=[^&\s]+", "[redacted endpoint]");
        logs.CreateLogger("Yap.Voice").LogWarning("Voice call failed ({ErrorType}): {Reason}", error.GetType().Name, reason);
        Error = error.Message switch
        {
            "Open Yap using its HTTPS address to use voice calls." => error.Message,
            "Voice calls are not supported by this browser. Try an updated browser." => error.Message,
            "Voice calls require a secure connection to Yap." => error.Message,
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
        { try { await media.StopAudioAsync(); } catch { } try { await media.CancelPreparedVoiceAsync(); } catch { } }
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
        try { await api.PostAsync($"api/chat/calls/{invite.Id}/end", new { }, timeout.Token); } catch { }
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
    private sealed record Configuration(bool Enabled);
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
        public bool Starting, Ended, LocalReady, PeerReady, Initiated, Muting, Notified;
    }
}
