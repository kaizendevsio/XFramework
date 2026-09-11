using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Clients;
using XFramework.Domain.Shared.BusinessObjects;
using Yap.Models;

namespace Yap.Services;

public sealed class ChatWorkspace(ICommunicationsChatClient client, IChatDirectory directory, ILogger<ChatWorkspace> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? threadLifetime;
    private ICommunicationsChatSession? session;
    private Task? refreshLoop;
    private int messagePages = 1;
    private int replyPages = 1;
    private readonly Dictionary<Guid, DateTime> typingMembers = [];
    private DateTime lastTypingPublished;
    private readonly Dictionary<Guid, string> memberNames = [];
    public event Action? Changed;
    public IReadOnlyList<ThreadListItemResponse> Conversations { get; private set; } = [];
    public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];
    public ChatMessage? ReplyParent { get; private set; }
    public IReadOnlyList<ChatMessage> Replies { get; private set; } = [];
    public int ReplyTotal { get; private set; }
    public GetThreadResponse? Selected { get; private set; }
    public ChatReferenceDataResponse? ReferenceData { get; private set; }
    public int ConversationTotal { get; private set; }
    public int MessageTotal { get; private set; }
    public Guid CredentialId => session?.CredentialId ?? Guid.Empty;
    public string? LiveError { get; private set; }
    public bool IsTyping { get { lock (typingMembers) return typingMembers.Values.Any(until => until > DateTime.UtcNow); } }
    public bool Ready => session is not null;
    public CancellationToken Cancellation => lifetime.Token;

    public async Task StartAsync(string deviceId)
    {
        if (session is not null) return;
        session = await client.ForCurrentActorAsync(deviceId, lifetime.Token);
        try
        {
            ReferenceData = Require(await session.EnsureChatDefaultsAsync(lifetime.Token));
            await RefreshAsync();
            await session.SubscribeUserEventsAsync(OnEventAsync, lifetime.Token);
            refreshLoop = ReconcileAsync();
        }
        catch { session = null; throw; }
    }

    public async Task RefreshAsync(bool moreConversations = false)
    {
        await gate.WaitAsync(lifetime.Token);
        try
        {
            await LoadConversationsAsync(moreConversations);
            if (Selected is not null) await LoadMessagesAsync();
            if (ReplyParent is not null) await LoadRepliesAsync();
            LiveError = null;
        }
        finally { gate.Release(); Changed?.Invoke(); }
    }

    public async Task SelectAsync(Guid id)
    {
        await gate.WaitAsync(lifetime.Token);
        try
        {
            if (Selected?.Id == id) return;
            threadLifetime?.Cancel();
            threadLifetime?.Dispose();
            threadLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            Selected = null;
            Messages = [];
            ClearReplies();
            lock (typingMembers) typingMembers.Clear();
            lastTypingPublished = DateTime.MinValue;
            messagePages = 1;
            Selected = Require(await Session.GetThreadAsync(id, lifetime.Token));
            await ResolveMemberNamesAsync(Selected.Members.Where(m => string.IsNullOrWhiteSpace(m.Alias)).Select(m => m.CredentialId));
            foreach (var member in Selected.Members.Where(m => string.IsNullOrWhiteSpace(m.Alias)))
                member.Alias = memberNames.GetValueOrDefault(member.CredentialId, "Workspace member");
            if (Selected.IsDirect)
                Selected.Name = Selected.Members.FirstOrDefault(m => m.CredentialId != CredentialId)?.Alias ?? "Direct message";
            await LoadMessagesAsync();
            await Session.SubscribeTypingAsync(id, state =>
            {
                if (state.CredentialId != CredentialId && Selected?.Id == state.ThreadId)
                {
                    lock (typingMembers)
                    {
                        if (state.IsTyping) typingMembers[state.CredentialId] = DateTime.UtcNow.AddSeconds(6);
                        else typingMembers.Remove(state.CredentialId);
                    }
                    Changed?.Invoke();
                    if (state.IsTyping) _ = ExpireTypingAsync(state.ThreadId, state.CredentialId);
                }
                return Task.CompletedTask;
            }, threadLifetime.Token);
        }
        finally { gate.Release(); Changed?.Invoke(); }
    }

    public async Task CloseConversationAsync()
    {
        await gate.WaitAsync(lifetime.Token);
        try { threadLifetime?.Cancel(); Selected = null; Messages = []; ClearReplies(); }
        finally { gate.Release(); Changed?.Invoke(); }
    }

    public async Task LoadEarlierAsync()
    {
        await gate.WaitAsync(lifetime.Token);
        try
        {
            messagePages++;
            try { await LoadMessagesAsync(); }
            catch { messagePages--; throw; }
        }
        finally { gate.Release(); Changed?.Invoke(); }
    }

    public async Task<Guid> SendAsync(Guid threadId, string text, Guid? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ChatOperationException("Write a message first.");
        // No automatic send retry: an interrupted RPC may already have committed a message.
        var response = Require(await Session.SendMessageAsync(threadId, text.Trim(), parentId, ct: lifetime.Token));
        return response.MessageId;
    }

    public async Task<SearchMessagesResponse> SearchAsync(string query, Guid? threadId, int page = 0)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ChatOperationException("Enter something to search for.");
        return Require(await Session.SearchMessagesAsync(query.Trim(), threadId, page, 30, lifetime.Token));
    }

    public async Task OpenMessageAsync(Guid threadId, Guid messageId)
    {
        await SelectAsync(threadId);
        while (Messages.All(m => m.Id != messageId) && Messages.Count < MessageTotal)
        {
            var count = Messages.Count;
            await LoadEarlierAsync();
            if (Messages.Count == count) break;
        }
        if (Messages.All(m => m.Id != messageId))
            throw new ChatOperationException("This message is no longer available. Search again to update the results.");
    }

    public async Task OpenRepliesAsync(ChatMessage parent)
    {
        await gate.WaitAsync(lifetime.Token);
        try
        {
            ReplyParent = parent; replyPages = 1; Replies = [];
            await LoadRepliesAsync();
        }
        catch { ClearReplies(); throw; }
        finally { gate.Release(); Changed?.Invoke(); }
    }

    private void ClearReplies() { ReplyParent = null; Replies = []; ReplyTotal = 0; }

    public async Task CloseRepliesAsync()
    {
        await gate.WaitAsync(lifetime.Token);
        try { ClearReplies(); }
        finally { gate.Release(); Changed?.Invoke(); }
    }

    public async Task LoadEarlierRepliesAsync()
    {
        await gate.WaitAsync(lifetime.Token);
        try
        {
            if (ReplyParent is null) return;
            replyPages++;
            try { await LoadRepliesAsync(); }
            catch { replyPages--; throw; }
        }
        finally { gate.Release(); Changed?.Invoke(); }
    }

    private async Task LoadRepliesAsync()
    {
        var items = new List<ThreadMessageItemResponse>();
        for (var page = 0; page < replyPages; page++)
        {
            var response = await Session.GetRepliesAsync(Selected!.Id, ReplyParent!.Id, page, 30, lifetime.Token);
            if (response.HttpStatusCode == System.Net.HttpStatusCode.NotFound) { ClearReplies(); return; }
            var result = Require(response);
            items.AddRange(result.Items);
            ReplyTotal = result.TotalCount;
            if (items.Count >= ReplyTotal) break;
        }
        Replies = MapMessages(items);
        ReplyParent = Messages.FirstOrDefault(m => m.Id == ReplyParent!.Id) ?? ReplyParent;
    }

    public async Task MarkReadAsync(Guid threadId, IReadOnlyCollection<Guid> messageIds)
    {
        if (messageIds.Count == 0) return;
        foreach (var batch in messageIds.Chunk(100))
            Require(await Session.MarkReadAsync(threadId, batch, lifetime.Token));
    }

    public async Task<Guid> CreateDirectAsync(Guid personId, string name)
        => Require(await Session.CreateDirectThreadAsync(personId, name: name, ct: lifetime.Token)).ThreadId;

    public async Task<Guid> CreateGroupAsync(string name, Guid typeId, IReadOnlyCollection<Guid> members)
        => Require(await Session.CreateThreadAsync(new CreateThreadRequest
        {
            Name = name.Trim(), TypeId = typeId, InitialMemberCredentialIds = members.ToList()
        }, lifetime.Token)).ThreadId;

    public async Task EditAsync(Guid threadId, Guid id, string text)
        => Require(await Session.EditMessageAsync(threadId, id, text.Trim(), lifetime.Token));
    public async Task DeleteAsync(Guid threadId, Guid id)
        => Require(await Session.DeleteMessageAsync(threadId, id, lifetime.Token));
    public async Task PinAsync(Guid threadId, ChatMessage message)
        => Require(message.Pinned
            ? await Session.UnpinMessageAsync(threadId, message.Id, lifetime.Token)
            : await Session.PinMessageAsync(threadId, message.Id, lifetime.Token));
    public async Task SaveAsync(Guid threadId, ChatMessage message)
        => Require(message.Saved
            ? await Session.UnsaveMessageAsync(threadId, message.Id, lifetime.Token)
            : await Session.SaveMessageAsync(threadId, message.Id, lifetime.Token));
    public async Task MuteAsync(Guid threadId, bool muted)
        => Require(await Session.MuteThreadAsync(threadId, muted, lifetime.Token));

    public async Task ToggleReactionAsync(Guid threadId, ChatMessage message, Guid typeId)
    {
        var ownReaction = message.Reactions.FirstOrDefault(r => r.TypeId == typeId)?.MyReactionId;
        Require(ownReaction is { } id
            ? await Session.DeleteReactionAsync(threadId, message.Id, id, lifetime.Token)
            : await Session.ReactAsync(threadId, message.Id, typeId, lifetime.Token));
    }

    public async Task PublishTypingAsync(bool typing)
    {
        if (Selected is null || (typing && DateTime.UtcNow - lastTypingPublished < TimeSpan.FromSeconds(4))) return;
        lastTypingPublished = DateTime.UtcNow;
        try { await Session.PublishTypingAsync(Selected.Id, typing, lifetime.Token); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogDebug(ex, "Typing update could not be published."); }
    }

    private async Task ExpireTypingAsync(Guid threadId, Guid credentialId)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(6), lifetime.Token);
            if (Selected?.Id != threadId) return;
            lock (typingMembers)
            {
                if (!typingMembers.TryGetValue(credentialId, out var until) || until > DateTime.UtcNow) return;
                typingMembers.Remove(credentialId);
            }
            Changed?.Invoke();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    private async Task LoadConversationsAsync(bool more)
    {
        var pageCount = Math.Max(1, (Conversations.Count + 29) / 30) + (more ? 1 : 0);
        var items = new List<ThreadListItemResponse>();
        for (var page = 0; page < pageCount; page++)
        {
            var result = Require(await Session.GetThreadsAsync(page, 30, lifetime.Token));
            items.AddRange(result.Items);
            ConversationTotal = result.TotalCount;
            if (items.Count >= result.TotalCount) break;
        }
        Conversations = items.DistinctBy(x => x.Id).ToArray();
        await ResolveMemberNamesAsync(Conversations.Where(c => c.IsDirect && c.OtherCredentialId.HasValue).Select(c => c.OtherCredentialId!.Value));
        foreach (var conversation in Conversations.Where(c => c.IsDirect && c.OtherCredentialId.HasValue))
            conversation.Name = memberNames.GetValueOrDefault(conversation.OtherCredentialId!.Value, "Direct message");
    }

    private async Task ResolveMemberNamesAsync(IEnumerable<Guid> credentialIds)
    {
        var missing = credentialIds.Distinct().Where(id => !memberNames.ContainsKey(id)).ToArray();
        if (missing.Length == 0) return;
        try
        {
            foreach (var person in await directory.ResolveAsync(missing, lifetime.Token))
                memberNames[person.Id] = person.Name;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogDebug(ex, "Member names are unavailable; chat remains accessible."); }
    }

    private async Task LoadMessagesAsync()
    {
        var id = Selected!.Id;
        var items = new List<ThreadMessageItemResponse>();
        for (var page = 0; page < messagePages; page++)
        {
            var result = Require(await Session.GetMessagesAsync(id, page, 50, lifetime.Token));
            items.AddRange(result.Items);
            MessageTotal = result.TotalCount;
            if (items.Count >= result.TotalCount) break;
        }
        Messages = MapMessages(items);
    }

    private ChatMessage[] MapMessages(IEnumerable<ThreadMessageItemResponse> items) => items.DistinctBy(m => m.Id).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => new ChatMessage(m.Id, m.SenderCredentialId,
                m.SenderCredentialId == CredentialId ? "You" : string.IsNullOrWhiteSpace(m.SenderAlias)
                    ? memberNames.GetValueOrDefault(m.SenderCredentialId, "Workspace member") : m.SenderAlias,
                m.Text, m.CreatedAt, m.SenderCredentialId == CredentialId, m.ParentMessageId, m.IsPinned, m.IsSaved)
                { Reactions = m.Reactions }).ToArray();

    private async Task OnEventAsync(CommunicationsRealtimeEvent evt)
    {
        // The wrapper acknowledges only after this refresh succeeds. Failure remains replayable.
        if (evt.TenantId != Session.TenantId) return;
        try { await RefreshAsync(); }
        catch (Exception)
        {
            LiveError = "Live updates were interrupted. Refresh to catch up.";
            Changed?.Invoke();
            throw;
        }
    }

    private async Task ReconcileAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync(lifetime.Token))
            {
                try { await RefreshAsync(); }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Yap could not reconcile chat history.");
                    LiveError = ex is UnauthorizedAccessException ? "Your session ended. Sign in again." : "Cannot reach your conversations. Try refreshing.";
                    Changed?.Invoke();
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    private ICommunicationsChatSession Session => session ?? throw new InvalidOperationException("Chat has not been initialized.");
    public static T Require<T>(QueryResponse<T> response)
    {
        if (!response.IsSuccess || response.Response is null) throw Failure((int)response.HttpStatusCode);
        return response.Response;
    }
    public static void Require(CmdResponse response)
    {
        if (!response.IsSuccess) throw Failure((int)response.HttpStatusCode);
    }
    private static ChatOperationException Failure(int status) => new(status switch
    {
        401 => "Your session ended. Please sign in again.",
        403 => "Your workspace permissions or chat policy don't allow this action.",
        404 => "This conversation or item is no longer available.",
        409 => "This item changed. Refresh and try again.",
        429 => "You're sending requests too quickly. Please wait a moment.",
        _ => "We couldn't complete that request. Refresh before trying again."
    });

    public async ValueTask DisposeAsync()
    {
        await lifetime.CancelAsync();
        threadLifetime?.Cancel();
        if (refreshLoop is not null) await refreshLoop;
        threadLifetime?.Dispose();
        lifetime.Dispose();
    }
}

public sealed class ChatOperationException(string message) : Exception(message);
