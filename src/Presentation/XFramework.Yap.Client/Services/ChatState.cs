using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed class ChatState(OfflineStore store, ChatApi api, IJSRuntime js) : IAsyncDisposable
{
    private readonly SemaphoreSlim sync = new(1, 1);
    private readonly SemaphoreSlim typingPublish = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private DotNetObjectReference<ChatState>? reference;
    private Task? polling;
    private int pages = 1;
    private int loadedLimit = 100, cachedCount;
    public bool HasEarlierMessages => Selected is { } selected && (cachedCount > selected.Messages.Count || Online && !NeedsLogin && selected.Messages.Count < selected.MessageTotal);
    private int inboxPages = 1;
    private bool refreshPending;
    private Task? refreshing;
    private readonly ConcurrentDictionary<Guid, DateTime> typing = new();
    private readonly HashSet<Guid> acknowledged = [];
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
    public void Notify() => Changed?.Invoke();
    public void DismissError() { Error = null; Notify(); }
    public void Report(Exception ex)
    {
        Console.Error.WriteLine($"Yap action failed: {ex}");
        Error = ex is ChatApiException ? ex.Message : "Could not finish this action. Check your connection and available device storage, then try again.";
        Notify();
    }

    public async Task InitializeAsync()
    {
        var saved = await store.SettingAsync("user");
        if (saved is not null) User = JsonSerializer.Deserialize<UserSession>(saved);
        if (User is not null) Conversations = await store.ConversationsAsync(Scope);
        Online = await js.InvokeAsync<bool>("yap.device.online");
        reference = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("yap.device.watch", reference);
        Ready = true;
        Notify();
        await SynchronizeAsync();
        polling = PollAsync();
    }

    private async Task PollAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        try { while (await timer.WaitForNextTickAsync(lifetime.Token)) await SynchronizeAsync(); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    [JSInvokable] public async Task ConnectivityChanged(bool online)
    { Online = online; if (!online) typing.Clear(); Notify(); if (online) await SynchronizeAsync(); }
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
                    for (var page = 0; page < inboxPages; page++)
                    {
                        var list = await api.GetAsync<ChatPage<Conversation>>($"api/chat/conversations?page={page}", lifetime.Token);
                        ConversationTotal = list.TotalCount;
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
        catch (ChatApiException ex) { NeedsLogin = ex.Status == 401; Report(ex); }
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
        if (!await js.InvokeAsync<bool>("yap.device.online")) { Online = false; Notify(); return; }
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
            api.Account = Scope;
            NeedsLogin = false;
            await store.SetSettingAsync("user", JsonSerializer.Serialize(User));
            Defaults ??= await api.PostAsync<ChatDefaults>("api/chat/initialize");
            await FlushAsync();
            for (var page = 0; page < inboxPages; page++)
            {
                var list = await api.GetAsync<ChatPage<Conversation>>($"api/chat/conversations?page={page}");
                ConversationTotal = list.TotalCount;
                await store.SaveConversationsAsync(Scope, list.Items);
                if ((page + 1) * 30 >= list.TotalCount) break;
            }
            Conversations = await store.ConversationsAsync(Scope);
            if (Selected is not null) await RefreshSelectedAsync(Selected.Id);
            await WatchEventsAsync();
        }
        catch (ChatApiException ex) { NeedsLogin = ex.Status == 401; if (ex.Status >= 500) Online = false; Error = ex.Message; }
        catch (HttpRequestException) { Online = false; Error = "Cannot reach chat. Your messages are saved and will retry."; }
        catch (TaskCanceledException) when (!lifetime.IsCancellationRequested) { Online = false; Error = "Chat took too long to respond. Saved messages will retry."; }
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
        await sync.WaitAsync();
        try
        {
            pages = 1; loadedLimit = 100;
            typing.Clear();
            acknowledged.Clear();
            Selected = Conversations.FirstOrDefault(x => x.Id == id) ?? new Conversation { Id = id };
            Selected.Messages = await store.MessagesAsync(Scope, id, limit: loadedLimit);
            cachedCount = await store.MessageCountAsync(Scope, id);
            ComposeReplies(Selected.Messages);
            Notify();
            if (Online && !NeedsLogin) await RefreshSelectedAsync(id);
            if (Online && !NeedsLogin) await WatchEventsAsync();
        }
        catch (Exception ex) { Report(ex); }
        finally { sync.Release(); Notify(); }
    }

    private async Task RefreshSelectedAsync(Guid id)
    {
        var conversation = await api.GetAsync<Conversation>($"api/chat/conversations/{id}");
        var summary = Conversations.FirstOrDefault(x => x.Id == id);
        conversation.LastMessageAt = summary?.LastMessageAt;
        conversation.Preview = summary?.Preview ?? "Start a conversation";
        conversation.Muted = summary?.Muted ?? false;
        var fetched = new List<ChatMessage>();
        var result = await api.GetAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{id}/messages?page=0");
        fetched.AddRange(result.Items); conversation.MessageTotal = result.TotalCount;
        await store.ReplaceWindowAsync(Scope, id, fetched, fetched.Count >= conversation.MessageTotal);
        conversation.Messages = await store.MessagesAsync(Scope, id, limit: loadedLimit);
        cachedCount = await store.MessageCountAsync(Scope, id);
        ComposeReplies(conversation.Messages);
        await store.SaveConversationsAsync(Scope, [conversation]);
        Selected = conversation;
        var index = Conversations.FindIndex(x => x.Id == id);
        if (index >= 0) Conversations[index] = conversation;
        Notify(); // Read receipts must not delay rendering a received message.
        var unread = fetched.Where(x => !x.Mine && !acknowledged.Contains(x.Id)).Select(x => x.Id).Take(100).ToList();
        if (unread.Count > 0 && conversation.Allows(ChatFeature.ReadReceipts))
        {
            await api.PostAsync("api/chat/read", new ReadMessages(id, unread));
            acknowledged.UnionWith(unread);
        }
    }

    public async Task LoadEarlierAsync()
    {
        await sync.WaitAsync();
        try
        {
            if (Selected is not { } conversation) return;
            if (cachedCount <= conversation.Messages.Count && Online && !NeedsLogin)
            {
                pages = Math.Max(1, conversation.Messages.Count / 50);
                var result = await api.GetAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{conversation.Id}/messages?page={pages}");
                await store.SaveMessagesAsync(Scope, result.Items); conversation.MessageTotal = result.TotalCount;
            }
            loadedLimit += 50;
            conversation.Messages = await store.MessagesAsync(Scope, conversation.Id, limit: loadedLimit);
            cachedCount = await store.MessageCountAsync(Scope, conversation.Id);
            ComposeReplies(conversation.Messages);
        }
        catch (Exception ex) { Report(ex); }
        finally { sync.Release(); Notify(); }
    }
    public async Task LoadMoreConversationsAsync() { inboxPages++; await SynchronizeAsync(); }

    public async Task<List<SearchHit>> SearchAsync(string query, Guid? thread = null)
    {
        if (query.Trim().Length < 2) return [];
        if (Online && !NeedsLogin)
            return (await api.GetAsync<ChatPage<SearchHit>>($"api/chat/search?query={Uri.EscapeDataString(query)}{(thread.HasValue ? $"&thread={thread}" : "")}")).Items;
        var results = new List<SearchHit>();
        foreach (var conversation in Conversations.Where(x => !thread.HasValue || x.Id == thread))
            results.AddRange((await store.MessagesAsync(Scope, conversation.Id)).Where(x => x.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(x => new SearchHit(x.ThreadId, x.Id, x.Text, x.CreatedAt)));
        return results.OrderByDescending(x => x.CreatedAt).Take(30).ToList();
    }

    public async Task LocateAsync(Guid id)
    {
        while (Selected is { } conversation && conversation.Messages.All(x => x.Id != id)
            && HasEarlierMessages) { var before = conversation.Messages.Count; await LoadEarlierAsync(); if (Selected?.Messages.Count == before) break; }
    }

    public async Task MuteAsync()
    {
        if (Selected is not { } conversation || !Online || NeedsLogin) return;
        try { await api.PostAsync("api/chat/thread-actions", new ThreadAction(conversation.Id, "mute", !conversation.Muted)); await SynchronizeAsync(); }
        catch (Exception ex) { Report(ex); }
    }

    public async Task LoadRepliesAsync(ChatMessage parent)
    {
        await sync.WaitAsync();
        try
        {
            if (Selected?.Id != parent.ThreadId) return;
            if (Online && !NeedsLogin)
            {
                var result = await api.GetAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{parent.ThreadId}/messages?parent={parent.Id}&page={parent.Replies.Count / 50}");
                await store.SaveMessagesAsync(Scope, result.Items);
            }
            Selected.Messages = await store.MessagesAsync(Scope, parent.ThreadId);
            loadedLimit = Math.Max(loadedLimit, Selected.Messages.Count);
            ComposeReplies(Selected.Messages);
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

    public Task<string> DraftAsync(string key) => store.DraftAsync(Scope, key);
    public Task SaveDraftAsync(string key, string text) => store.SaveDraftAsync(Scope, key, text);
    public Task SaveBeforeUpdateAsync() => store.UseAsync(_ => Task.FromResult(true));

    public async Task SendAsync(Guid thread, string text, Guid? parent, string draftKey, PickedFile? file = null, bool threadReply = false)
    {
        await sync.WaitAsync();
        try
        {
        if (User is null) throw new InvalidOperationException("Sign in before sending a message.");
        var id = Guid.NewGuid();
        var content = string.IsNullOrWhiteSpace(text) ? file?.Name ?? "" : text.Trim();
        if (content.Length is 0 or > 4000) throw new InvalidOperationException("Write a message up to 4,000 characters.");
        var message = new ChatMessage { Id = id, ThreadId = thread, SenderId = User.CredentialId,
            Text = content, CreatedAt = DateTime.UtcNow, Mine = true, ParentId = parent, Delivery = "Queued",
            LocalFileKey = file?.Key, HasAttachments = file is not null, IsThreadReply = threadReply,
            Attachments = file is null ? [] : [new ChatAttachment(id, file.Name, file.ContentType, file.Size)] };
        await store.QueueAsync(new QueuedMessage { Scope = Scope, Id = id, ThreadId = thread, Text = content,
            ParentId = parent, CreatedTicks = message.CreatedAt.Ticks, FileKey = file?.Key, FileName = file?.Name,
            ContentType = file?.ContentType, FileSize = file?.Size ?? 0 }, message, draftKey);
        if (Selected?.Id == thread) { Selected.Messages.Add(message); ComposeReplies(Selected.Messages); }
        PendingCount++; Notify();
        }
        finally { sync.Release(); }
        _ = SynchronizeAsync();
    }

    private async Task FlushAsync()
    {
        foreach (var item in await store.PendingAsync(Scope))
        {
            if (item.Paused) continue;
            try
            {
                if (!item.MessageConfirmed)
                {
                    var receipt = await api.PostAsync<MessageReceipt>("api/chat/messages", new SendMessage(item.Id, item.ThreadId, item.Text, item.ParentId, (await store.MessageAsync(Scope, item.Id))?.IsThreadReply == true));
                    if (receipt?.MessageId != item.Id) throw new InvalidOperationException("Message receipt did not match.");
                    item.MessageConfirmed = true; await store.SaveQueueAsync(item);
                }
                if (item.FileKey is not null)
                {
                    if (item.StorageId is null)
                    {
                        var upload = await js.InvokeAsync<UploadReceipt>("yap.device.upload", item.FileKey, item.FileName,
                            item.ThreadId, api.Token, Scope, item.ContentType);
                        if (upload.Status is < 200 or >= 300) throw new ChatApiException(upload.Status);
                        item.StorageId = upload.Id; await store.SaveQueueAsync(item);
                    }
                    await api.PostAsync("api/chat/attachments", new AttachMessageFile(item.ThreadId, item.Id, item.StorageId.Value));
                }
                await store.CompleteQueueAsync(item);
                if (item.FileKey is not null) await js.InvokeVoidAsync("yap.device.removeFile", item.FileKey);
            }
            catch (ChatApiException ex) when (ex.Status is >= 400 and < 500 && ex.Status is not (401 or 408 or 429))
            { item.Paused = true; item.Error = ex.Message; await store.SaveQueueAsync(item); Error = ex.Message; }
        }
    }

    public async Task RetryAsync()
    {
        foreach (var item in await store.PendingAsync(Scope)) { item.Paused = false; item.Error = null; await store.SaveQueueAsync(item); }
        Online = await js.InvokeAsync<bool>("yap.device.online"); await SynchronizeAsync();
    }

    public async Task ActionAsync(ChatMessage message, string action, string? text = null, string? emoji = null)
    {
        if (!Online || NeedsLogin) { Error = "Connect and sign in to change a message."; Notify(); return; }
        try
        {
            var reaction = Defaults?.Reactions.FirstOrDefault(x => x.Emoji == emoji);
            var mine = emoji is null ? (Guid?)null : message.MyReactionIds.GetValueOrDefault(emoji);
            if (action == "react" && mine.HasValue && mine.Value != Guid.Empty) action = "unreact";
            await api.PostAsync("api/chat/message-actions", new MessageAction(message.ThreadId, message.Id, action, text, reaction?.Id, mine));
            await SynchronizeAsync();
        }
        catch (Exception ex) { Report(ex); }
    }

    public async Task<List<Person>> SearchPeopleAsync(string text) => !Online || text.Trim().Length < 2 ? []
        : await api.GetAsync<List<Person>>($"api/chat/people?search={Uri.EscapeDataString(text)}");
    public async Task<Guid> CreateAsync(string name, List<Guid> people, bool group)
    {
        var result = await api.PostAsync<CreatedConversation>("api/chat/conversations", new CreateConversation(name, people, group));
        await SynchronizeAsync(); return result!.Id;
    }

    public async Task UpdateConversationAsync(ConversationUpdate update)
    { await api.PostAsync("api/chat/conversation-settings", update); await SynchronizeAsync(); }
    public async Task ChangeMemberAsync(ConversationMemberAction action)
    { await api.PostAsync("api/chat/conversation-members", action); await SynchronizeAsync(); }

    public async Task LoadAttachmentMetadataAsync(ChatMessage message)
    {
        if (!Online || NeedsLogin) return;
        message.Attachments = await api.GetAsync<List<ChatAttachment>>($"api/chat/conversations/{message.ThreadId}/messages/{message.Id}/attachments");
        await store.SaveMessagesAsync(Scope, [message]);
    }

    public async Task OpenAttachmentsAsync(ChatMessage message)
    {
        try
        {
            if (Online && !NeedsLogin)
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
            var key = $"{Scope.Replace(':', '-')}-{file.Id:N}";
            await js.InvokeVoidAsync("yap.device.openFile", key, file.Name,
                $"api/chat/conversations/{message.ThreadId}/messages/{message.Id}/attachments/{file.Id}", Scope, Online && !NeedsLogin);
        }
        catch (Exception ex) { Report(ex); }
    }

    public async Task LogoutAsync()
    {
        await sync.WaitAsync();
        try
        {
            await store.SetSettingAsync("pendingLogout", "true");
            await store.ClearPrivateAsync();
            User = null; Selected = null; Conversations = []; Defaults = null; PendingCount = 0; NeedsLogin = false;
            typing.Clear(); publishingThread = null;
            api.Account = "";
            await js.InvokeVoidAsync("yap.device.events", "");
            await js.InvokeVoidAsync("yap.device.clearFiles");
        }
        finally { sync.Release(); Notify(); }
        await SynchronizeAsync();
    }

    public async ValueTask DisposeAsync()
    { lifetime.Cancel(); if (polling is not null) await polling; reference?.Dispose(); lifetime.Dispose(); }
    public sealed record PickedFile(string Key, string Name, string ContentType, long Size);
    private sealed record UploadReceipt(int Status, Guid Id);
    private sealed record CreatedConversation(Guid Id);
}
