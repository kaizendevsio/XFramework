using System.Text.Json;
using Microsoft.JSInterop;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class ChatState
{
    private readonly Dictionary<Guid, (string Scope, HashSet<Guid> Ids, HashSet<Guid> Created)> messageUpdates = [];
    private static readonly JsonSerializerOptions SocketJson = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<Guid, HashSet<Guid>> deliveredPending = [];
    private Task? delivering;
    private long reconciliationFailures;

    private async Task ReconcileSocketAsync(string scope)
    {
        Encryption.InvalidateRecipients();
        var failures = reconciliationFailures;
        await RefreshHint();
        if (!Online || NeedsLogin || Scope != scope || failures != reconciliationFailures)
            throw new HttpRequestException("Chat reconciliation is incomplete.");
        RetryDelivered();
    }

    private async Task SaveInboxSnapshotAsync(List<Conversation> conversations, long version)
    {
        await messageChanges.WaitAsync(lifetime.Token);
        try
        {
            if (version != socketMessageVersion) { refreshPending = true; return; }
            await store.SaveConversationsAsync(Scope, conversations);
        }
        finally { messageChanges.Release(); }
    }

    [JSInvokable] public async Task ChatSocketEvent(string scope, string json)
    {
        if (scope != Scope || User is null) throw new OperationCanceledException("The signed-in account changed.");
        var item = JsonSerializer.Deserialize<ChatSocketEvent>(json, SocketJson)
            ?? throw new InvalidOperationException("Invalid chat event.");
        if (item.Kind == "refresh")
        {
            await ReconcileSocketAsync(scope);
            return;
        }
        if (item.Kind == "typing" && item.Body is { } typingBody)
        {
            var update = typingBody.Deserialize<TypingUpdate>(SocketJson)!;
            TypingChanged(update.ThreadId, update.CredentialId, update.IsTyping);
            return;
        }
        if (item.Kind == "call" && item.Body is { } callBody)
        {
            var call = callBody.Deserialize<YapCallEvent>(SocketJson);
            if (call is not null && CallReceived is not null) await CallReceived(call);
            return;
        }
        if (item.ActorId == User.CredentialId && item.Kind is "MessagesRead" or "MessagesDelivered")
        {
            if (item.Kind == "MessagesRead" && Selected?.Id != item.ThreadId) await ReconcileSocketAsync(scope);
            return;
        }
        if (item.Kind is not ("MessageCreated" or "MessageEdited" or "MessageUpdated" or "ReactionCreated" or "ReactionDeleted" or "MessagesRead" or "MessagesDelivered"))
            throw new InvalidOperationException("Unsupported chat event.");
        if (item.ThreadId is not { } thread || item.MessageIds is not { Count: > 0 and <= 50 } ids
            || ids.Contains(Guid.Empty) || item.Messages is not { } messages
            || messages.Count != ids.Distinct().Count() || messages.Any(m => m.ThreadId != thread || !ids.Contains(m.Id)))
            throw new InvalidOperationException("Invalid message update.");
        if (!Conversations.Any(c => c.Id == thread) || messages.Any(m => m.IsThreadReply))
        { await ReconcileSocketAsync(scope); return; }
        var created = item.Kind == "MessageCreated" ? ids.ToHashSet() : [];
        await DecryptMessagesAsync(messages);
        await messageChanges.WaitAsync(lifetime.Token);
        try
        {
            if (scope != Scope || User is null) throw new OperationCanceledException();
            socketMessageVersion++;
            await ApplyMessageUpdatesAsync(thread, ids, created, messages);
        }
        finally { messageChanges.Release(); }
    }

    private void QueueDelivered(Guid thread, IEnumerable<ChatMessage> messages)
    {
        var ids = messages.Where(m => !m.Mine && !m.EncryptionLocked && !m.EncryptionPending).Select(m => m.Id).ToList();
        if (ids.Count == 0) return;
        if (!deliveredPending.TryGetValue(thread, out var pending)) deliveredPending[thread] = pending = [];
        pending.UnionWith(ids);
        RetryDelivered();
    }

    private void RetryDelivered()
    {
        if (deliveredPending.Count > 0 && delivering is not { IsCompleted: false }) delivering = DeliverPendingAsync(Scope);
    }

    private async Task DeliverPendingAsync(string scope)
    {
        try
        {
            // Coalesce acknowledgments without delaying the next incoming message.
            await Task.Delay(30, lifetime.Token);
            while (scope == Scope && deliveredPending.Count > 0)
            {
                var entry = deliveredPending.First();
                var ids = entry.Value.Take(50).ToList();
                await api.PostAsync("api/chat/delivered", new ReadMessages(entry.Key, ids), lifetime.Token);
                if (scope != Scope) return;
                entry.Value.ExceptWith(ids);
                if (entry.Value.Count == 0) deliveredPending.Remove(entry.Key);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { await RecordErrorAsync(ex); } // Reconnect/catch-up retries the watermark quietly.
    }

    [JSInvokable] public Task ChatEvent(string scope, string json)
    {
        if (scope != Scope || User is null) return Task.CompletedTask;
        ChatUpdateHint? hint;
        try { hint = JsonSerializer.Deserialize<ChatUpdateHint>(json); }
        catch (JsonException) { return RefreshHint(); }
        if (hint is null || hint.ThreadId == Guid.Empty || hint.MessageIds is not { Count: > 0 and <= 50 }
            || hint.MessageIds.Contains(Guid.Empty) || hint.Kind is not ("MessageCreated" or "MessageEdited" or "ReactionCreated" or "ReactionDeleted" or "MessagesRead" or "MessagesDelivered"))
            return RefreshHint();
        // Our own reads/deliveries do not change our outbound receipts. The read
        // action already updates local unread state; other participants still fetch.
        if (hint.ActorId == User.CredentialId && hint.Kind is "MessagesRead" or "MessagesDelivered")
            return hint.Kind == "MessagesRead" && Selected?.Id != hint.ThreadId ? RefreshHint() : Task.CompletedTask;
        if (!Conversations.Any(c => c.Id == hint.ThreadId)) return RefreshHint();
        if (!messageUpdates.TryGetValue(hint.ThreadId, out var pending))
            messageUpdates[hint.ThreadId] = pending = (scope, [], []);
        pending.Ids.UnionWith(hint.MessageIds);
        if (hint.Kind == "MessageCreated") pending.Created.UnionWith(hint.MessageIds);
        if (pending.Ids.Count > 50 || messageUpdates.Count > 20)
        { messageUpdates.Clear(); return RefreshHint(); }
        return refreshing is { IsCompleted: false } ? refreshing : refreshing = RefreshEventsAsync();
    }

    private async Task RefreshMessagesAsync(Guid thread, List<Guid> ids, HashSet<Guid> created)
    {
        var result = await api.GetAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{thread}/messages?" + string.Join('&', ids.Select(id => $"ids={id}")), lifetime.Token);
        // Missing items can mean deletion, hiding or a membership/policy change.
        if (result.Items.Count != ids.Count || result.Items.Any(m => m.ThreadId != thread || !ids.Contains(m.Id) || m.IsThreadReply))
        { refreshPending = true; return; }
        await DecryptMessagesAsync(result.Items);
        await messageChanges.WaitAsync(lifetime.Token);
        try { await ApplyMessageUpdatesAsync(thread, ids, created, result.Items); }
        finally { messageChanges.Release(); }
    }

    private async Task ApplyMessageUpdatesAsync(Guid thread, List<Guid> ids, HashSet<Guid> created, List<ChatMessage> items)
    {
        var version = selectionVersion;
        var mutation = messageMutationVersion;
        var pending = (await store.PendingAsync(Scope)).Select(x => x.Id).ToHashSet();
        var updates = items.Where(m => !pending.Contains(m.Id)).ToList();
        if (updates.Count == 0) return;
        var previous = await store.MessageUpdatesAsync(Scope, ids);
        var known = previous.Select(m => m.Id).ToHashSet();
        // A receipt/edit for uncached history must not insert a disconnected old
        // row into the paginated cache or count it as a newly received message.
        updates = updates.Where(m => known.Contains(m.Id) || created.Contains(m.Id)).ToList();
        if (updates.Count == 0) return;
        var movedReaders = updates.SelectMany(m => m.LatestReaders).Select(p => p.Id).ToHashSet();
        var latestOwn = updates.Any(m => m.IsLatestOwnMessage);
        var changed = new List<ChatMessage>(updates);
        var conversation = Conversations.FirstOrDefault(c => c.Id == thread);
        if (version == selectionVersion && mutation == messageMutationVersion && Selected?.Id == thread)
        {
            var selected = Selected;
            foreach (var message in selected.Messages.Where(m => !ids.Contains(m.Id)))
            {
                var moved = message.LatestReaders.RemoveAll(p => movedReaders.Contains(p.Id)) > 0;
                if (latestOwn && message.IsLatestOwnMessage) { message.IsLatestOwnMessage = false; moved = true; }
                if (moved) changed.Add(message);
            }
            foreach (var message in updates)
            {
                var index = selected.Messages.FindIndex(m => m.Id == message.Id);
                if (index >= 0) { message.Replies = selected.Messages[index].Replies; selected.Messages[index] = message; }
                else if (historyOffset == 0) selected.Messages.Add(message);
            }
            selected.Messages = selected.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).TakeLast(HistoryWindowSize).ToList();
            selected.MessageTotal += updates.Count(m => !known.Contains(m.Id));
            ComposeReplies(selected.Messages);
            MergeStaging(selected);
            ApplyOptimisticMessages(selected);
        }
        await store.SaveMessagesAsync(Scope, changed);
        if (Selected?.Id == thread)
        {
            cachedCount += updates.Count(m => !known.Contains(m.Id));
            if (historyOffset > 0 && Selected.Messages.LastOrDefault() is { } anchor)
                historyOffset = await store.MessageOffsetAsync(Scope, thread, anchor.Id);
        }
        if (conversation is not null)
        {
            var latest = updates.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).First();
            if (conversation.LastMessageAt is null || latest.CreatedAt >= conversation.LastMessageAt)
            { conversation.LastMessage = latest; conversation.LastMessageAt = latest.CreatedAt; conversation.Preview = MessagePreview(latest); }
            if (Selected?.Id != thread) conversation.Unread += updates.Count(m => !m.Mine && !known.Contains(m.Id));
            await store.SaveConversationsAsync(Scope, [conversation]);
        }
        if (mutation != messageMutationVersion) refreshPending = true;
        QueueDelivered(thread, updates);
        Notify();
    }
}
