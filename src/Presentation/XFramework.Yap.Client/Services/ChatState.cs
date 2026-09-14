using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class ChatState(OfflineStore store, ChatApi api, IJSRuntime js) : IAsyncDisposable
{
    public ChatEncryption Encryption { get; } = new(api, js);
    public bool EncryptionEnabled { get; private set; }
    private readonly SemaphoreSlim sync = new(1, 1);
    private readonly SemaphoreSlim localChanges = new(1, 1);
    private Task? sending;
    private bool sendRequested;
    private readonly SemaphoreSlim typingPublish = new(1, 1);
    private readonly SemaphoreSlim readReceipts = new(1, 1);
    private readonly HashSet<Guid> activeActions = [];
    private readonly CancellationTokenSource lifetime = new();
    private DotNetObjectReference<ChatState>? reference;
    private Task? polling;
    private int pages = 1;
    private const int HistoryWindowSize = 100;
    private int historyOffset, cachedCount;
    private Guid? replyParent;
    private int replyOffset, replyPages, replyCached;
    public bool HasNewerReplies => replyOffset > 0;
    public bool HasEarlierReplies(ChatMessage parent) => replyCached > replyOffset + parent.Replies.Count || Online && !NeedsLogin && replyCached < parent.ReplyTotal;
    public bool HasNewerMessages => historyOffset > 0;
    public bool HasEarlierMessages => Selected is { } selected && (cachedCount > historyOffset + selected.Messages.Count || Online && !NeedsLogin && cachedCount < selected.MessageTotal);
    private int inboxPages = 1;
    private bool refreshPending;
    private Task? refreshing;
    private readonly ConcurrentDictionary<Guid, DateTime> typing = new();
    private readonly HashSet<Guid> acknowledged = [];
    private Guid? viewedConversation;
    private long selectionVersion;
    private DateTime lastTyping;
    private Guid? publishingThread;
    public string? TypingText
    {
        get
        {
            var people = typing.Where(x => x.Value > DateTime.UtcNow).Select(x => x.Key).ToArray();
            return people.Length == 0 ? null : string.Join(", ", people
                .Select(id => Selected?.People.FirstOrDefault(x => x.Id == id)?.Name ?? "Someone"))
                + (people.Length == 1 ? " is typing…" : " are typing…");
        }
    }
    public int ConversationTotal { get; private set; }
    public bool HasMoreConversations => inboxPages * 30 < ConversationTotal;
    public event Action<Guid>? ConversationRemoved;
    public List<Person> TypingPeople => typing.Where(x => x.Value > DateTime.UtcNow)
        .Select(x => Selected?.People.FirstOrDefault(p => p.Id == x.Key) ?? new Person(x.Key, "Someone", "")).ToList();
    public UserSession? User { get; private set; }
    public bool Ready { get; private set; }
    public bool Online { get; private set; } = true;
    public bool NeedsLogin { get; private set; }
    public bool Busy { get; private set; }
    public string? Error { get; private set; }
    public int PendingCount { get; private set; }
    public List<Conversation> Conversations { get; private set; } = [];
    public Conversation? Selected { get; private set; }
    public ChatDefaults? Defaults { get; private set; }
    public string Scope => User is null ? "" : OfflineStore.Scope(User);
    public event Action? Changed;
    public event Func<YapCallEvent, Task>? CallReceived;
    [JSInvokable] public Task VoiceEvent(string json)
    {
        var call = JsonSerializer.Deserialize<YapCallEvent>(json);
        return call is not null && CallReceived is not null ? CallReceived(call) : Task.CompletedTask;
    }
    public void Notify() => Changed?.Invoke();
    public async Task RefreshProfileAsync()
    {
        var session = await api.GetAsync<SessionResponse>("api/session");
        if (session.User is not null && OfflineStore.Scope(session.User) == Scope)
        { User = session.User; await store.SetSettingAsync("user", JsonSerializer.Serialize(User)); }
        api.Token = session.AntiforgeryToken;
        Notify();
    }
    public void DismissError() { Error = null; Notify(); }
    public static bool IsConnectionFailure(Exception ex) => ex is HttpRequestException or TaskCanceledException
        || ex is ChatApiException { Status: 408 or 429 or >= 500 };
    private void SetOffline() { Online = false; typing.Clear(); }
    public void Report(Exception ex)
    {
        if (IsConnectionFailure(ex)) { SetOffline(); Notify(); return; }
        Console.Error.WriteLine($"Yap action failed: {ex}");
        _ = RecordErrorAsync(ex);
        Error = ex is ChatApiException ? ex.Message : "Could not finish this action. Check your connection and available device storage, then try again.";
        Notify();
    }
    private async Task RecordErrorAsync(Exception ex)
    {
        try { await js.InvokeVoidAsync("yap.diagnostics.record", "dotnet.error", new { type = ex.GetType().FullName, status = (ex as ChatApiException)?.Status }); }
        catch { /* Diagnostics must never interfere with chat or error recovery. */ }
    }

    public async Task InitializeAsync()
    {
        await js.InvokeVoidAsync("yap.diagnostics.record", "startup.stage", new { phase = "saved-account" });
        var saved = await store.SettingAsync("user");
        if (saved is not null) User = JsonSerializer.Deserialize<UserSession>(saved);
        await js.InvokeVoidAsync("yap.diagnostics.record", "startup.stage", new { phase = "saved-conversations" });
        if (User is not null) Conversations = await store.ConversationsAsync(Scope);
        Online = await js.InvokeAsync<bool>("yap.device.online");
        reference = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("yap.device.watch", reference);
        Ready = true;
        Notify();
        // Opening cached conversations must not wait for network sync or key registration.
        polling = SynchronizeAndPollAsync();
    }

    private async Task SynchronizeAndPollAsync()
    {
        try
        {
            await js.InvokeVoidAsync("yap.diagnostics.record", "startup.stage", new { phase = "background-sync" });
            await SynchronizeAsync();
            await PollAsync();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Report(ex); }
    }

    private async Task PollAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        try { while (await timer.WaitForNextTickAsync(lifetime.Token)) await SynchronizeAsync(); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    [JSInvokable] public async Task ConnectivityChanged(bool online)
    {
        // Browser connectivity is only a hint; the server must answer before we reconnect.
        if (online) await SynchronizeAsync();
        else { SetOffline(); Notify(); }
    }
    [JSInvokable] public Task RefreshHint()
    {
        refreshPending = true;
        return refreshing is { IsCompleted: false } ? refreshing : refreshing = RefreshEventsAsync();
    }

    private async Task RefreshEventsAsync()
    {
        try
        {
            await sync.WaitAsync(lifetime.Token);
            try
            {
                while (refreshPending && Online && !NeedsLogin && User is not null)
                {
                    refreshPending = false;
                    // Show the open conversation before fetching inbox summaries.
                    if (Selected is not null) await RefreshSelectedAsync(Selected.Id);
                    await SynchronizeDeletedConversationsAsync();
                    for (var page = 0; page < inboxPages; page++)
                    {
                        var list = await api.GetAsync<ChatPage<Conversation>>($"api/chat/conversations?page={page}", lifetime.Token);
                        ConversationTotal = list.TotalCount;
                        await ResolvePreviewsAsync(list.Items);
                        await store.SaveConversationsAsync(Scope, list.Items);
                        if ((page + 1) * 30 >= list.TotalCount) break;
                    }
                    Conversations = await store.ConversationsAsync(Scope);
                    Notify();
                }
            }
            finally { sync.Release(); }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (ChatApiException ex) { await HandleApiFailureAsync(ex); Notify(); }
        catch (Exception ex) { Report(ex); }
    }

    [JSInvokable] public void TypingChanged(Guid thread, Guid credential, bool active)
    {
        if (!Online || NeedsLogin || Selected?.Id != thread || User?.CredentialId == credential) return;
        if (active) { typing[credential] = DateTime.UtcNow.AddSeconds(6); _ = ExpireTypingAsync(thread, credential); }
        else typing.TryRemove(credential, out _);
        Notify();
    }

    private async Task ExpireTypingAsync(Guid thread, Guid credential)
    {
        try { await Task.Delay(6000, lifetime.Token); }
        catch (OperationCanceledException) { return; }
        if (Selected?.Id == thread && typing.TryGetValue(credential, out var until) && until <= DateTime.UtcNow)
        { ((ICollection<KeyValuePair<Guid, DateTime>>)typing).Remove(new(credential, until)); Notify(); }
    }

    public async Task PublishTypingAsync(Guid thread, bool active)
    {
        await typingPublish.WaitAsync();
        try
        {
            if (!Online || NeedsLogin || User is null || Selected?.Allows(ChatFeature.Typing) == false) return;
            if (active && publishingThread == thread && DateTime.UtcNow - lastTyping < TimeSpan.FromSeconds(3)) return;
            if (!active && publishingThread != thread) return;
            publishingThread = active ? thread : null;
            lastTyping = DateTime.UtcNow;
            try { await api.PostAsync("api/chat/thread-actions", new ThreadAction(thread, "typing", active)); }
            catch (Exception) { /* Typing is transient; message delivery remains independent. */ }
        }
        finally { typingPublish.Release(); }
    }

    private ValueTask WatchEventsAsync() => js.InvokeVoidAsync("yap.device.events", Scope, Selected?.Id);

    public async Task SynchronizeAsync()
    {
        if (!await js.InvokeAsync<bool>("yap.device.online")) { SetOffline(); Notify(); return; }
        await sync.WaitAsync(lifetime.Token);
        Busy = true;
        try
        {
            var session = await api.GetAsync<SessionResponse>("api/session", lifetime.Token);
            Online = true;
            Error = null;
            api.Token = session.AntiforgeryToken;
            if (await store.SettingAsync("pendingLogout") == "true")
            {
                await api.AuthenticateAsync("logout", []);
                await store.SetSettingAsync("pendingLogout", "false");
                session = await api.GetAsync<SessionResponse>("api/session", lifetime.Token);
                api.Token = session.AntiforgeryToken;
            }
            if (session.User is null) { NeedsLogin = User is not null; return; }
            if (User is not null && OfflineStore.Scope(session.User) != Scope)
            {
                // A different cookie identity must never receive this account's outbox.
                NeedsLogin = true;
                Error = "Another account signed in. Sign out here before switching accounts.";
                return;
            }
            User = session.User;
            EncryptionEnabled = session.EncryptionRequired;
            api.Account = Scope;
            NeedsLogin = false;
            await store.SetSettingAsync("user", JsonSerializer.Serialize(User));
            Defaults ??= await api.PostAsync<ChatDefaults>("api/chat/initialize");
            if (EncryptionEnabled) await Encryption.EnsureAsync(User);
            await FinishEncryptionResetAsync();
            await SynchronizeDeletedConversationsAsync();
            await FlushAsync();
            await CompleteDeferredDeliveriesAsync();
            for (var page = 0; page < inboxPages; page++)
            {
                var list = await api.GetAsync<ChatPage<Conversation>>($"api/chat/conversations?page={page}");
                ConversationTotal = list.TotalCount;
                await ResolvePreviewsAsync(list.Items);
                await store.SaveConversationsAsync(Scope, list.Items);
                if ((page + 1) * 30 >= list.TotalCount) break;
            }
            Conversations = await store.ConversationsAsync(Scope);
            if (Selected is not null) await RefreshSelectedAsync(Selected.Id);
            await WatchEventsAsync();
        }
        catch (ChatApiException ex) { await HandleApiFailureAsync(ex); }
        catch (HttpRequestException) { SetOffline(); }
        catch (TaskCanceledException) when (!lifetime.IsCancellationRequested) { SetOffline(); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Report(ex); }
        finally
        {
            if (User is not null) PendingCount = (await store.PendingAsync(Scope)).Count;
            Busy = false; sync.Release(); Notify();
        }
    }

    public async Task SelectAsync(Guid id)
    {
        viewedConversation = id;
        var version = ++selectionVersion;
        var scope = Scope;
        try
        {
            // Local storage has its own gate. Never wait for a slow network sync
            // before showing the conversation and its bounded cached history.
            var messages = await store.MessagesAsync(scope, id, limit: HistoryWindowSize);
            var count = await store.MessageCountAsync(scope, id);
            if (version != selectionVersion || Scope != scope) return;
            pages = 1; historyOffset = 0; replyParent = null;
            foreach (var old in Conversations) old.Messages.Clear();
            typing.Clear();
            acknowledged.Clear();
            var conversation = Conversations.FirstOrDefault(x => x.Id == id) ?? new Conversation { Id = id };
            conversation.Messages = messages;
            Selected = conversation; cachedCount = count;
            ComposeReplies(conversation.Messages);
            Notify();
            await sync.WaitAsync(lifetime.Token);
            try
            {
                if (version != selectionVersion || Scope != scope) return;
                if (Online && !NeedsLogin) await RefreshSelectedAsync(id);
                if (Online && !NeedsLogin) await WatchEventsAsync();
            }
            finally { sync.Release(); }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Report(ex); }
        finally { Notify(); }
    }

    private async Task HandleApiFailureAsync(ChatApiException error)
    {
        if (IsConnectionFailure(error)) { SetOffline(); return; }
        Error = error.Message;
        if (error.Status != 401) return;
        try
        {
            // An upstream API rejection is not proof that the browser session ended.
            var session = await api.GetAsync<SessionResponse>("api/session", lifetime.Token);
            NeedsLogin = session.User is null || User is null || OfflineStore.Scope(session.User) != Scope;
            api.Token = session.AntiforgeryToken;
            if (!NeedsLogin) { Error = null; SetOffline(); }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ChatApiException)
        {
            // If session validation itself is unavailable, retain the last known state.
            Error = null; SetOffline();
        }
    }

    private async Task RefreshSelectedAsync(Guid id)
    {
        var version = selectionVersion;
        Conversation conversation;
        try { conversation = await api.GetAsync<Conversation>($"api/chat/conversations/{id}"); }
        catch (ChatApiException ex) when (ex.Status is 403 or 404) { Report(ex); return; }
        var summary = Conversations.FirstOrDefault(x => x.Id == id);
        conversation.LastMessageAt = summary?.LastMessageAt;
        conversation.Preview = summary?.Preview ?? "Start a conversation";
        conversation.LastMessage = summary?.LastMessage;
        conversation.Muted = summary?.Muted ?? false;
        var fetched = new List<ChatMessage>();
        var result = await api.GetAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{id}/messages?page=0");
        fetched.AddRange(result.Items); conversation.MessageTotal = result.TotalCount;
        await DecryptMessagesAsync(fetched);
        await store.ReplaceWindowAsync(Scope, id, fetched, fetched.Count >= conversation.MessageTotal);
        if (historyOffset > 0 && Selected?.Messages.LastOrDefault() is { } anchor)
            historyOffset = await store.MessageOffsetAsync(Scope, id, anchor.Id);
        conversation.Messages = await store.MessagesAsync(Scope, id, limit: HistoryWindowSize, skip: historyOffset);
        cachedCount = await store.MessageCountAsync(Scope, id);
        ComposeReplies(conversation.Messages);
        await RestoreReplyWindowAsync(conversation);
        await store.SaveConversationsAsync(Scope, [conversation]);
        if (version != selectionVersion || viewedConversation != id) return;
        Selected = conversation;
        var index = Conversations.FindIndex(x => x.Id == id);
        if (index >= 0) Conversations[index] = conversation;
        Notify();
    }

    public void LeaveConversation(Guid id)
    {
        if (viewedConversation != id) return;
        viewedConversation = null;
        selectionVersion++;
        Selected?.Messages.Clear();
        foreach (var conversation in Conversations) conversation.Messages.Clear();
        Selected = null;
        typing.Clear();
        _ = WatchEventsAsync();
    }

    public async Task ReadVisibleAsync(Guid id, Guid? parent = null)
    {
        await readReceipts.WaitAsync(lifetime.Token);
        try
        {
            if (!Online || NeedsLogin || viewedConversation != id || Selected is not { } conversation || !conversation.Allows(ChatFeature.ReadReceipts)) return;
            var version = selectionVersion;
            var visible = await js.InvokeAsync<Guid[]>("yap.device.visibleMessages", id, parent);
            if (version != selectionVersion || viewedConversation != id) return;
            var messages = parent is { } root ? conversation.Messages.FirstOrDefault(x => x.Id == root)?.Replies ?? [] : conversation.Messages;
            var unread = messages.Where(x => !x.Mine && !x.EncryptionPending && !x.EncryptionLocked && visible.Contains(x.Id) && !acknowledged.Contains(x.Id)).Select(x => x.Id).Take(100).ToList();
            if (unread.Count == 0) return;
            await api.PostAsync("api/chat/read", new ReadMessages(id, unread), lifetime.Token);
            acknowledged.UnionWith(unread);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { await RecordErrorAsync(ex); }
        finally { readReceipts.Release(); }
    }

    public async Task LoadEarlierAsync()
    {
        await sync.WaitAsync();
        try
        {
            if (Selected is not { } conversation) return;
            while (cachedCount <= historyOffset + conversation.Messages.Count && Online && !NeedsLogin && pages * 50 < conversation.MessageTotal)
            {
                // Reply/search caches may contain gaps: row count is not a server page cursor.
                var result = await api.GetAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{conversation.Id}/messages?page={pages}");
                await DecryptMessagesAsync(result.Items);
                await store.SaveMessagesAsync(Scope, result.Items); conversation.MessageTotal = result.TotalCount;
                pages++;
                cachedCount = await store.MessageCountAsync(Scope, conversation.Id);
            }
            cachedCount = await store.MessageCountAsync(Scope, conversation.Id);
            historyOffset = Math.Min(historyOffset + 50, Math.Max(0, cachedCount - HistoryWindowSize));
            conversation.Messages = await store.MessagesAsync(Scope, conversation.Id, limit: HistoryWindowSize, skip: historyOffset);
            ComposeReplies(conversation.Messages);
        }
        catch (Exception ex) { Report(ex); }
        finally { sync.Release(); Notify(); }
    }
    public async Task LoadNewerAsync()
    {
        await sync.WaitAsync();
        try
        {
            if (Selected is not { } conversation || historyOffset == 0) return;
            historyOffset = Math.Max(0, historyOffset - 50);
            conversation.Messages = await store.MessagesAsync(Scope, conversation.Id, limit: HistoryWindowSize, skip: historyOffset);
            ComposeReplies(conversation.Messages);
        }
        catch (Exception ex) { Report(ex); }
        finally { sync.Release(); Notify(); }
    }
    public async Task LoadMoreConversationsAsync() { inboxPages++; await SynchronizeAsync(); }

    public async Task<List<SearchHit>> SearchAsync(string query, Guid? thread = null)
    {
        if (query.Trim().Length < 2) return [];
        if (Online && !NeedsLogin && !EncryptionEnabled)
            return (await api.GetAsync<ChatPage<SearchHit>>($"api/chat/search?query={Uri.EscapeDataString(query)}{(thread.HasValue ? $"&thread={thread}" : "")}")).Items;
        var results = new List<SearchHit>();
        foreach (var conversation in Conversations.Where(x => !thread.HasValue || x.Id == thread))
        {
            for (var offset = 0; ; offset += 100)
            {
                var batch = await store.MessagesAsync(Scope, conversation.Id, limit: 100, skip: offset);
                results.AddRange(batch.Where(x => !x.EncryptionLocked && x.Text.Contains(query, StringComparison.OrdinalIgnoreCase)).Select(x => new SearchHit(x.ThreadId, x.Id, x.Text, x.CreatedAt)));
                results = results.OrderByDescending(x => x.CreatedAt).Take(30).ToList();
                if (batch.Count < 100) break;
            }
        }
        return results.OrderByDescending(x => x.CreatedAt).Take(30).ToList();
    }

    public async Task LocateAsync(Guid id)
    {
        await sync.WaitAsync();
        try
        {
            if (Selected is { } selected && selected.Messages.All(x => x.Id != id)
                && await store.MessageAsync(Scope, id) is { } cached && cached.ThreadId == selected.Id)
            {
                historyOffset = Math.Max(0, await store.MessageOffsetAsync(Scope, selected.Id, id) - HistoryWindowSize / 2);
                selected.Messages = await store.MessagesAsync(Scope, selected.Id, limit: HistoryWindowSize, skip: historyOffset);
                ComposeReplies(selected.Messages);
            }
        }
        finally { sync.Release(); Notify(); }
        while (Selected is { } conversation && conversation.Messages.All(x => x.Id != id)
            && HasEarlierMessages) { var before = conversation.Messages.FirstOrDefault()?.Id; await LoadEarlierAsync(); if (Selected?.Messages.FirstOrDefault()?.Id == before) break; }
    }

    public async Task MuteAsync()
    {
        if (Selected is not { } conversation || !Online || NeedsLogin) return;
        try { await api.PostAsync("api/chat/thread-actions", new ThreadAction(conversation.Id, "mute", !conversation.Muted)); await SynchronizeAsync(); }
        catch (Exception ex) { Report(ex); }
    }

    public async Task LoadRepliesAsync(ChatMessage parent, bool newer = false)
    {
        await sync.WaitAsync();
        try
        {
            if (Selected?.Id != parent.ThreadId) return;
            var first = replyParent != parent.Id;
            if (first) { replyParent = parent.Id; replyOffset = 0; replyPages = 0; }
            replyCached = await store.ReplyCountAsync(Scope, parent.Id);
            while (!newer && Online && !NeedsLogin && (first || replyCached <= replyOffset + parent.Replies.Count) && (replyPages == 0 || replyPages * 50 < parent.ReplyTotal))
            {
                var result = await api.GetAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{parent.ThreadId}/messages?parent={parent.Id}&page={replyPages}");
                await DecryptMessagesAsync(result.Items);
                await store.SaveMessagesAsync(Scope, result.Items);
                parent.ReplyTotal = result.TotalCount; replyPages++;
                replyCached = await store.ReplyCountAsync(Scope, parent.Id);
                if (first) break;
            }
            // Thread replies have their own bounded window, independent of the main timeline.
            replyCached = await store.ReplyCountAsync(Scope, parent.Id);
            if (newer) replyOffset = Math.Max(0, replyOffset - 50);
            else if (!first) replyOffset = Math.Min(replyOffset + 50, Math.Max(0, replyCached - HistoryWindowSize));
            parent.Replies = await store.MessagesAsync(Scope, parent.ThreadId, limit: HistoryWindowSize, skip: replyOffset, parent: parent.Id);
        }
        catch (Exception ex) { Report(ex); }
        finally { sync.Release(); Notify(); }
    }

    private static void ComposeReplies(List<ChatMessage> messages)
    {
        var lookup = messages.ToDictionary(x => x.Id);
        foreach (var message in messages) message.Replies = [];
        foreach (var message in messages.Where(x => x.ParentId.HasValue))
            if (lookup.TryGetValue(message.ParentId!.Value, out var parent))
            { parent.Replies.Add(message); message.Quote = new(parent.Id, parent.Sender, parent.Text); }
        foreach (var message in messages) message.ReplyTotal = Math.Max(message.ReplyTotal, message.Replies.Count);
    }

    private async Task RestoreReplyWindowAsync(Conversation conversation)
    {
        if (replyParent is not { } parentId || conversation.Messages.FirstOrDefault(x => x.Id == parentId) is not { } parent) return;
        var previous = Selected?.Messages.FirstOrDefault(x => x.Id == parentId);
        if (replyOffset > 0 && previous?.Replies.LastOrDefault() is { } anchor)
            replyOffset = await store.MessageOffsetAsync(Scope, conversation.Id, anchor.Id, parentId);
        replyCached = await store.ReplyCountAsync(Scope, parentId);
        parent.Replies = await store.MessagesAsync(Scope, conversation.Id, limit: HistoryWindowSize, skip: replyOffset, parent: parentId);
        parent.ReplyTotal = Math.Max(parent.ReplyTotal, Math.Max(previous?.ReplyTotal ?? 0, replyCached));
    }

    public Task<string> DraftAsync(string key) => store.DraftAsync(Scope, key);
    public Task SaveDraftAsync(string key, string text) => store.SaveDraftAsync(Scope, key, text);
    public Task SaveBeforeUpdateAsync() => store.UseAsync(_ => Task.FromResult(true));

    private async Task SynchronizeDeletedConversationsAsync()
    {
        var known = Conversations.Select(c => c.Id).ToHashSet();
        foreach (var item in await store.PendingAsync(Scope)) known.Add(item.ThreadId);
        if (Selected is not null) known.Add(Selected.Id);
        if (known.Count == 0) return;
        for (var page = 0; ; page++)
        {
            var removed = await api.GetAsync<ChatPage<Guid>>($"api/chat/conversations/deleted?page={page}", lifetime.Token);
            foreach (var id in removed.Items.Where(known.Contains)) await ForgetDeletedConversationAsync(id);
            if ((page + 1) * 100 >= removed.TotalCount) break;
        }
    }

    private async Task ForgetDeletedConversationAsync(Guid thread)
    {
        foreach (var item in (await store.PendingAsync(Scope)).Where(x => x.ThreadId == thread && x.FileKey is not null))
            await js.InvokeVoidAsync("yap.device.removeFile", item.FileKey);
        await store.RemoveConversationAsync(Scope, thread, lifetime.Token, discardPending: true);
        Conversations.RemoveAll(x => x.Id == thread);
        if (Selected?.Id == thread) { Selected = null; typing.Clear(); }
        PendingCount = (await store.PendingAsync(Scope)).Count;
        Error = null;
        ConversationRemoved?.Invoke(thread);
    }

    public async Task DeleteConversationAsync(Guid thread, bool everyone = false)
    {
        if (!Online || NeedsLogin) throw new InvalidOperationException("Connect and sign in before removing a conversation.");
        await sync.WaitAsync(lifetime.Token);
        try
        {
            if ((await store.PendingAsync(Scope)).Any(x => x.ThreadId == thread))
                throw new InvalidOperationException("Wait for this conversation's messages to finish sending before removing it.");
            await api.PostAsync("api/chat/thread-actions", new ThreadAction(thread, everyone ? "delete-for-everyone" : "delete-for-me", true));
            await store.RemoveConversationAsync(Scope, thread, lifetime.Token);
            Conversations.RemoveAll(x => x.Id == thread);
            if (Selected?.Id == thread) { Selected = null; typing.Clear(); await WatchEventsAsync(); }
        }
        finally { sync.Release(); Notify(); }
    }

    public async Task SendAsync(Guid thread, string text, Guid? parent, string draftKey, PickedFile? file = null, bool threadReply = false)
    {
        await localChanges.WaitAsync(lifetime.Token);
        try
        {
            if (User is null) throw new InvalidOperationException("Sign in before sending a message.");
            if (Encryption.Status.ResetHistoryPending) throw new InvalidOperationException("Wait for encryption setup to finish before sending.");
            if (EncryptionEnabled && !Encryption.Status.Approved) throw new InvalidOperationException("Unlock this device in Settings before sending encrypted messages.");
            var id = Guid.NewGuid();
            var content = string.IsNullOrWhiteSpace(text) ? file?.Name ?? "" : text.Trim();
            if (content.Length is 0 or > 4000) throw new InvalidOperationException("Write a message up to 4,000 characters.");
            var message = new ChatMessage { Id = id, ThreadId = thread, SenderId = User.CredentialId,
                Text = content, CreatedAt = DateTime.UtcNow, Mine = true, ParentId = parent, Delivery = "Queued",
                LocalFileKey = file?.Key, HasAttachments = file is not null, IsThreadReply = threadReply,
                Attachments = file is null ? [] : [new ChatAttachment(id, file.Name, file.ContentType, file.Size)] };
            await store.QueueAsync(new QueuedMessage { Scope = Scope, Id = id, ThreadId = thread, Text = content,
                ParentId = parent, CreatedTicks = message.CreatedAt.Ticks, FileKey = file?.Key, FileName = file?.Name,
                ContentType = file?.ContentType, FileSize = file?.Size ?? 0, UploadId = file?.UploadId }, message, draftKey, expectedDraft: text);
            if (Selected?.Id == thread && historyOffset == 0) { Selected.Messages.Add(message); if (Selected.Messages.Count > HistoryWindowSize) Selected.Messages.RemoveAt(0); ComposeReplies(Selected.Messages); }
            PendingCount++; Notify();
        }
        finally { localChanges.Release(); }
        sendRequested = true;
        if (sending is not { IsCompleted: false }) sending = SynchronizeSendsAsync();
    }

    private async Task SynchronizeSendsAsync()
    {
        try
        {
            do
            {
                sendRequested = false;
                await FlushSendsAsync();
            } while (sendRequested && PendingCount > 0 && Online && !NeedsLogin);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Report(ex); }
    }

    private async Task FlushSendsAsync()
    {
        // A live session already has its account binding, CSRF token and encryption
        // identity. Sending only needs to drain the outbox; normal sync/SSE updates
        // inbox summaries and receipts independently.
        var requiresSync = false;
        await sync.WaitAsync(lifetime.Token);
        try
        {
            requiresSync = !Online || NeedsLogin || User is null || api.Account != Scope
                || string.IsNullOrEmpty(api.Token) || Defaults is null
                || EncryptionEnabled && !Encryption.Status.Approved;
            if (!requiresSync) await FlushAsync();
        }
        catch (ChatApiException ex) { await HandleApiFailureAsync(ex); }
        catch (Exception ex) when (IsConnectionFailure(ex)) { SetOffline(); }
        finally { sync.Release(); Notify(); }
        if (requiresSync) await SynchronizeAsync();
    }

    private async Task FlushAsync(bool allowRosterRetry = true)
    {
        var rosterChanged = false;
        var attempted = new HashSet<Guid>();
        var stop = false;
        while (!stop)
        {
            var batch = (await store.PendingAsync(Scope)).Where(item => !attempted.Contains(item.Id)).ToList();
            if (batch.Count == 0) break;
            foreach (var item in batch)
            {
                attempted.Add(item.Id);
                // Older clients paused an attachment if its successful response was lost.
                // The same stored-file link can now be retried idempotently.
                if (item.Paused && item.MessageConfirmed && item.StorageId.HasValue &&
                    item.Error == "This change conflicts with a message already saved. Review it before retrying.")
                { item.Paused = false; item.Error = null; await store.SaveQueueAsync(item); }
                if (item.Paused) continue;
                try
                {
                    if (!item.MessageConfirmed)
                    {
                        var message = await store.MessageAsync(Scope, item.Id) ?? throw new InvalidOperationException("Saved message is unavailable.");
                        List<Guid>? recipients = null;
                        if (EncryptionEnabled)
                        {
                            if (!Encryption.Status.Approved) { stop = true; break; }
                            var directories = await Encryption.RecipientsAsync(User!, item.ThreadId, allowPending: true);
                            recipients = directories.Select(x => x.GetProperty("credentialId").GetGuid()).ToList();
                            if (message.EncryptedEnvelope is null)
                            {
                                if (message.AcceptedSenderDirectoryRevision.HasValue && (message.AcceptedSenderDirectoryRevision != Encryption.Status.DirectoryRevision
                                    || !message.RecipientDirectoryRevisions.OrderBy(x => x.Key).SequenceEqual(directories
                                        .Select(x => new KeyValuePair<Guid, long>(x.GetProperty("credentialId").GetGuid(), x.GetProperty("revision").GetInt64())).OrderBy(x => x.Key))))
                                {
                                    // No envelope has been posted yet. Re-encrypt the file if its original
                                    // signed device revision changed during an interrupted upload.
                                    item.StorageId = null; await store.SaveQueueAsync(item);
                                }
                                message.EncryptionSenderDeviceId = Encryption.Status.DeviceId;
                                message.AcceptedSenderDirectoryRevision = Encryption.Status.DirectoryRevision;
                                message.RecipientDirectoryRevisions = directories.ToDictionary(x => x.GetProperty("credentialId").GetGuid(), x => x.GetProperty("revision").GetInt64());
                                await store.SaveMessagesAsync(Scope, [message]);
                            }
                            if (item.FileKey is not null && item.StorageId is null)
                                await UploadEncryptedAttachmentAsync(item, message, directories);
                            if (item.StorageId is { } encryptedFile)
                                message.Attachments = [new(encryptedFile, item.FileName!, item.ContentType!, item.FileSize,
                                    message.Attachments.FirstOrDefault(x => x.Id == encryptedFile)?.EncryptionSenderDeviceId ?? message.EncryptionSenderDeviceId,
                                    message.Attachments.FirstOrDefault(x => x.Id == encryptedFile)?.SenderDirectoryRevision ?? message.AcceptedSenderDirectoryRevision,
                                    message.Attachments.FirstOrDefault(x => x.Id == encryptedFile)?.Key)];
                            if (message.EncryptedEnvelope is null)
                            {
                                message.EncryptedEnvelope = await Encryption.EncryptAsync(User!, message, directories);
                                // Persist randomized ciphertext before the request: response loss must not create a different retry.
                                await store.SaveMessagesAsync(Scope, [message]);
                            }
                        }
                        var receipt = await api.PostAsync<MessageReceipt>("api/chat/messages", new SendMessage(item.Id, item.ThreadId,
                            message.EncryptedEnvelope is null ? item.Text : "Encrypted message", item.ParentId, message.IsThreadReply,
                            message.EncryptedEnvelope, message.EncryptedEnvelope is null ? recipients : message.RecipientDirectoryRevisions.Keys.ToList(), message.EncryptionSenderDeviceId, message.AcceptedSenderDirectoryRevision, message.RecipientDirectoryRevisions));
                        if (receipt?.MessageId != item.Id) throw new InvalidOperationException("Message receipt did not match.");
                        item.MessageConfirmed = true; await store.SaveQueueAsync(item);
                    }
                    if (item.FileKey is not null && item.StorageId is null)
                    {
                        if (item.UploadId is { } session)
                        {
                            // A streamed attachment already has every part in the bucket; only sealing remains.
                            var stored = await api.PostAsync<StoredFile>($"api/chat/uploads/session/{session}/complete");
                            item.StorageId = stored!.Id;
                        }
                        else
                        {
                            var upload = await js.InvokeAsync<UploadReceipt>("yap.device.upload", item.FileKey, item.FileName,
                                item.ThreadId, api.Token, Scope, item.ContentType);
                            if (upload.Status is < 200 or >= 300) throw new ChatApiException(upload.Status);
                            item.StorageId = upload.Id;
                        }
                        await store.SaveQueueAsync(item);
                    }
                    if (item.StorageId is { } storage)
                        await api.PostAsync("api/chat/attachments", new AttachMessageFile(item.ThreadId, item.Id, storage));
                    await store.CompleteQueueAsync(item);
                    if (Selected?.Messages.FirstOrDefault(message => message.Id == item.Id) is { } visible) visible.Delivery = "Sent";
                    PendingCount = Math.Max(0, PendingCount - 1);
                    Notify();
                    if (item.FileKey is not null) await js.InvokeVoidAsync("yap.device.removeFile", item.FileKey);
                }
                catch (ChatApiException ex) when (ex.Status == 428 && !item.MessageConfirmed)
                {
                    // A recipient who has not opened the encrypted app yet has no public keys.
                    // Keep retrying this conversation without blocking the rest of the outbox.
                    item.Error = "Waiting for encrypted setup";
                    await store.SaveQueueAsync(item);
                }
                catch (ChatApiException ex) when (ex.Status == 412 && !item.MessageConfirmed)
                {
                    // The server returns 412 only after finding no previously accepted message ID.
                    // Refresh the roster and retry once immediately, without duplicating a send.
                    var pending = await store.MessageAsync(Scope, item.Id);
                    if (pending is not null) { pending.EncryptedEnvelope = null; await store.SaveMessagesAsync(Scope, [pending]); }
                    rosterChanged = true;
                }
                catch (ChatApiException ex) when (ex.Status is >= 400 and < 500 && ex.Status is not (401 or 408 or 428 or 429))
                { item.Paused = true; item.Error = ex.Message; await store.SaveQueueAsync(item); Error = ex.Message; }
                catch (Exception ex) when (Transient(ex))
                {
                    // The message stays queued and its own bubble already says so. A backend
                    // blip that the next sync clears is not worth interrupting the reader,
                    // and the rest of the queue is behind this one anyway.
                    Console.Error.WriteLine($"Yap send will retry: {ex.Message}");
                    if (ex is HttpRequestException or TaskCanceledException) SetOffline();
                    stop = true;
                    break;
                }
            }
        }
        if (rosterChanged && allowRosterRetry) await FlushAsync(allowRosterRetry: false);
    }

    private int deferredDeliveryPage;
    private async Task CompleteDeferredDeliveriesAsync()
    {
        if (!EncryptionEnabled || User is null || !Encryption.Status.Approved) return;
        try
        {
            var pending = await api.GetAsync<DeferredDeliveryPage>($"api/chat/encryption/pending?page={deferredDeliveryPage}");
            deferredDeliveryPage = (deferredDeliveryPage + 1) * 20 >= pending.TotalCount ? 0 : deferredDeliveryPage + 1;
            var threadDirectories = new Dictionary<Guid, JsonElement[]>();
            foreach (var item in pending.Items)
            {
                try
                {
                    var message = item.Message;
                    if (!threadDirectories.TryGetValue(message.ThreadId, out var current))
                        threadDirectories[message.ThreadId] = current = await Encryption.RecipientsAsync(User, message.ThreadId, allowPending: true);
                    var directories = current.Where(x => message.EncryptionAudienceCredentialIds.Contains(x.GetProperty("credentialId").GetGuid())).ToArray();
                    var ready = directories.Select(x => x.GetProperty("credentialId").GetGuid()).ToHashSet();
                    if (item.PendingCredentialIds.Count == message.PendingEncryptionCount && !item.PendingCredentialIds.Any(ready.Contains)) continue;
                    await Encryption.DecryptAsync(User, [message]);
                    if (message.EncryptionLocked) continue;
                    message.EncryptionSenderDeviceId = Encryption.Status.DeviceId;
                    message.AcceptedSenderDirectoryRevision = Encryption.Status.DirectoryRevision;
                    var envelope = await Encryption.EncryptAsync(User, message, directories);
                    await api.PostAsync("api/chat/encryption/complete", new
                    {
                        threadId = message.ThreadId, messageId = message.Id, expectedEnvelopeHash = item.EnvelopeHash,
                        encryptedEnvelope = envelope, encryptionSenderDeviceId = message.EncryptionSenderDeviceId,
                        senderDirectoryRevision = message.AcceptedSenderDirectoryRevision,
                        recipientDirectoryRevisions = directories.ToDictionary(x => x.GetProperty("credentialId").GetGuid(), x => x.GetProperty("revision").GetInt64())
                    });
                }
                catch (ChatApiException ex) when (ex.Status is 403 or 404 or 409 or 412) { /* Membership/edit changed; refresh next sync. */ }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { await RecordErrorAsync(ex); } // Catch-up must never block sending or opening the inbox.
    }

    private async Task DecryptMessagesAsync(List<ChatMessage> messages)
    {
        if (User is null) return;
        var encrypted = messages.Where(x => x.EncryptedEnvelope is not null).ToList();
        if (encrypted.Count == 0) return;
        var ids = encrypted.Select(x => x.Id).ToArray();
        var cached = await store.UseAsync(async db => (await db.Messages.AsNoTracking()
            .Where(x => x.Scope == Scope && ids.Contains(x.Id)).ToListAsync())
            .Select(x => JsonSerializer.Deserialize<ChatMessage>(x.Json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!).ToDictionary(x => x.Id));
        var pending = new List<ChatMessage>();
        foreach (var message in encrypted)
        {
            if (!message.EncryptionPending && cached.TryGetValue(message.Id, out var prior) && !prior.EncryptionLocked && prior.EncryptedEnvelope == message.EncryptedEnvelope
                && (!prior.HasAttachments || prior.Attachments.Count > 0)
                && prior.AcceptedSenderDirectoryRevision == message.AcceptedSenderDirectoryRevision && prior.EncryptionSenderDeviceId == message.EncryptionSenderDeviceId
                && prior.SenderId == message.SenderId && prior.ThreadId == message.ThreadId && prior.ParentId == message.ParentId && prior.IsThreadReply == message.IsThreadReply)
            { message.Text = prior.Text; message.Attachments = prior.Attachments; message.HasAttachments = prior.HasAttachments; }
            else pending.Add(message);
        }
        await Encryption.DecryptAsync(User, pending);
    }

    private async Task UploadEncryptedAttachmentAsync(QueuedMessage item, ChatMessage message, JsonElement[] directories)
    {
        if (item.UploadId.HasValue) throw new InvalidOperationException("Reattach this file to encrypt it before sending.");
        var upload = await js.InvokeAsync<UploadReceipt>("yap.device.uploadEncrypted", item.FileKey, item.ThreadId,
            api.Token, Scope, ChatEncryption.Context(User!, message, "attachment"), directories,
            item.ContentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true);
        if (upload.Status is < 200 or >= 300) throw new ChatApiException(upload.Status);
        message.Attachments = [new(upload.Id, item.FileName!, item.ContentType!, item.FileSize,
            message.EncryptionSenderDeviceId, message.AcceptedSenderDirectoryRevision, upload.Key)];
        await store.SaveMessagesAsync(Scope, [message]);
        item.StorageId = upload.Id;
        await store.SaveQueueAsync(item);
    }

    /// <summary>A failure the outbox can simply try again, as opposed to a rejection.</summary>
    private static bool Transient(Exception ex) => ex switch
    {
        ChatApiException api => api.Status is >= 500 or 408 or 428 or 429,
        HttpRequestException or TaskCanceledException => true,
        JSException => true,
        _ => false
    };

    public async Task RetryAsync()
    {
        foreach (var item in await store.PendingAsync(Scope)) { item.Paused = false; item.Error = null; await store.SaveQueueAsync(item); }
        await SynchronizeAsync();
    }

    public async Task ActionAsync(ChatMessage message, string action, string? text = null, string? emoji = null)
    {
        if (!Online || NeedsLogin) { Error = "Connect and sign in to change a message."; Notify(); return; }
        if (!activeActions.Add(message.Id)) return;
        try
        {
            var reaction = Defaults?.Reactions.FirstOrDefault(x => x.Emoji == emoji);
            var mine = emoji is null ? (Guid?)null : message.MyReactionIds.GetValueOrDefault(emoji);
            if (action == "react" && mine.HasValue && mine.Value != Guid.Empty) action = "unreact";
            if (action == "edit" && (EncryptionEnabled || message.EncryptedEnvelope is not null))
            {
                var directories = await Encryption.RecipientsAsync(User!, message.ThreadId, message.EncryptionAudienceCredentialIds.Count > 0, message.EncryptionAudienceCredentialIds);
                var edited = new ChatMessage { Id = message.Id, ThreadId = message.ThreadId, SenderId = message.SenderId,
                    ParentId = message.ParentId, IsThreadReply = message.IsThreadReply, Text = text ?? "", Attachments = message.Attachments,
                    EncryptionSenderDeviceId = Encryption.Status.DeviceId, AcceptedSenderDirectoryRevision = Encryption.Status.DirectoryRevision };
                var envelope = await Encryption.EncryptAsync(User!, edited, directories);
                await api.PostAsync("api/chat/message-actions", new MessageAction(message.ThreadId, message.Id, action, "Encrypted message",
                    EncryptedEnvelope: envelope, EncryptionSenderDeviceId: edited.EncryptionSenderDeviceId, SenderDirectoryRevision: edited.AcceptedSenderDirectoryRevision,
                    RecipientDirectoryRevisions: directories.ToDictionary(x => x.GetProperty("credentialId").GetGuid(), x => x.GetProperty("revision").GetInt64())));
            }
            else await api.PostAsync("api/chat/message-actions", new MessageAction(message.ThreadId, message.Id, action, text, reaction?.Id, mine));
            await SynchronizeAsync();
        }
        catch (ChatApiException ex) when (ex.Status == 409 && action is "react" or "unreact" or "pin" or "unpin" or "save" or "unsave") { await SynchronizeAsync(); }
        catch (Exception ex) { Report(ex); }
        finally { activeActions.Remove(message.Id); }
    }

    public async Task<List<Person>> SearchPeopleAsync(string text) => !Online || text.Trim().Length < 2 ? []
        : await api.GetAsync<List<Person>>($"api/chat/people?search={Uri.EscapeDataString(text)}");
    public async Task<Guid> CreateAsync(string name, List<Guid> people, bool group)
    {
        var result = await api.PostAsync<CreatedConversation>("api/chat/conversations", new CreateConversation(name, people, group));
        await SynchronizeAsync(); return result!.Id;
    }

    public async Task ToggleFavoriteAsync(Conversation conversation)
    {
        var favorite = !conversation.IsFavorite;
        await store.SetFavoriteAsync(Scope, conversation.Id, favorite);
        conversation.IsFavorite = favorite;
        Changed?.Invoke();
    }

    public Task<Conversation> ConversationDetailsAsync(Guid id) => api.GetAsync<Conversation>($"api/chat/conversations/{id}");

    public async Task UpdateConversationAsync(ConversationUpdate update)
    { await api.PostAsync("api/chat/conversation-settings", update); await SynchronizeAsync(); }
    public async Task ChangeMemberAsync(ConversationMemberAction action)
    { await api.PostAsync("api/chat/conversation-members", action); await SynchronizeAsync(); }

    public async Task LoadAttachmentMetadataAsync(ChatMessage message)
    {
        if (message.EncryptedEnvelope is not null) return;
        if (!Online || NeedsLogin) return;
        message.Attachments = await api.GetAsync<List<ChatAttachment>>($"api/chat/conversations/{message.ThreadId}/messages/{message.Id}/attachments");
        await store.SaveMessagesAsync(Scope, [message]);
    }

    public async Task OpenAttachmentsAsync(ChatMessage message)
    {
        try
        {
            if (Online && !NeedsLogin && message.EncryptedEnvelope is null)
            {
                message.Attachments = await api.GetAsync<List<ChatAttachment>>($"api/chat/conversations/{message.ThreadId}/messages/{message.Id}/attachments");
                await store.SaveMessagesAsync(Scope, [message]);
            }
            if (message.Attachments.Count == 0) { Error = "No attachments are saved for this message."; Notify(); }
        }
        catch (Exception ex) { Report(ex); }
    }

    public async Task OpenFileAsync(ChatMessage message, ChatAttachment file)
    {
        try
        {
            var key = message.EncryptedEnvelope is not null ? await PrepareEncryptedFileAsync(message, file) : $"{Scope.Replace(':', '-')}-{file.Id:N}";
            await js.InvokeVoidAsync("yap.device.openFile", key, file.Name,
                $"api/chat/conversations/{message.ThreadId}/messages/{message.Id}/attachments/{file.Id}", Scope, Online && !NeedsLogin);
        }
        catch (Exception ex) { Report(ex); }
    }

    public async Task<string> PrepareEncryptedFileAsync(ChatMessage message, ChatAttachment file)
    {
        if (message.EncryptionLocked || User is null) throw new InvalidOperationException("Unlock this encrypted message first.");
        var key = $"{Scope.Replace(':', '-')}-{file.Id:N}.verified";
        var context = ChatEncryption.Context(User, new ChatMessage { Id = message.Id, ThreadId = message.ThreadId,
            SenderId = message.SenderId, ParentId = message.ParentId, IsThreadReply = message.IsThreadReply,
            EncryptionSenderDeviceId = file.EncryptionSenderDeviceId ?? message.EncryptionSenderDeviceId,
            AcceptedSenderDirectoryRevision = file.SenderDirectoryRevision ?? message.AcceptedSenderDirectoryRevision }, "attachment");
        if (await js.InvokeAsync<bool>("yap.device.hasVerifiedFile", key, Scope, context)) return key;
        if (!Online) throw new HttpRequestException("This attachment is not saved on this device yet.");
        var directory = await api.GetAsync<JsonElement>(ChatEncryption.SenderDirectoryPath(message.SenderId, file.EncryptionSenderDeviceId ?? message.EncryptionSenderDeviceId));
        await js.InvokeVoidAsync("yap.device.decryptFile", key,
            $"api/chat/conversations/{message.ThreadId}/messages/{message.Id}/attachments/{file.Id}?storageId=true", Scope,
            Online && !NeedsLogin, context, directory, file.Key);
        return key;
    }

    public async Task LogoutAsync()
    {
        await sync.WaitAsync();
        await localChanges.WaitAsync();
        try
        {
            Encryption.Reset(); EncryptionEnabled = false;
            await store.SetSettingAsync("pendingLogout", "true");
            await store.ClearPrivateAsync();
            User = null; Selected = null; Conversations = []; Defaults = null; PendingCount = 0; NeedsLogin = false;
            typing.Clear(); publishingThread = null;
            api.Account = "";
            await js.InvokeVoidAsync("yap.device.events", "");
            await js.InvokeVoidAsync("yap.device.clearFiles");
        }
        finally { localChanges.Release(); sync.Release(); Notify(); }
        await SynchronizeAsync();
    }

    public async ValueTask DisposeAsync()
    { lifetime.Cancel(); if (polling is not null) await polling; if (sending is not null) await sending; reference?.Dispose(); lifetime.Dispose(); }
    public sealed record PickedFile(string Key, string Name, string ContentType, long Size, bool Staged = true, Guid? UploadId = null);
    private sealed record UploadReceipt(int Status, Guid Id, EncryptedAttachmentKey? Key = null);
    private sealed record CreatedConversation(Guid Id);
}
