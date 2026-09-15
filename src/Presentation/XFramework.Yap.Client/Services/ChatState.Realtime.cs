using System.Text.Json;
using Microsoft.JSInterop;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class ChatState
{
    private readonly Dictionary<Guid, (string Scope, HashSet<Guid> Ids, HashSet<Guid> Created)> messageUpdates = [];

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
        if (hint.ActorId == User.CredentialId && hint.Kind is "MessagesRead" or "MessagesDelivered") return Task.CompletedTask;
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
        var version = selectionVersion;
        var mutation = messageMutationVersion;
        var result = await api.GetAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{thread}/messages?" + string.Join('&', ids.Select(id => $"ids={id}")), lifetime.Token);
        // Missing items can mean deletion, hiding or a membership/policy change.
        if (result.Items.Count != ids.Count || result.Items.Any(m => m.ThreadId != thread || !ids.Contains(m.Id) || m.IsThreadReply))
        { refreshPending = true; return; }
        var pending = (await store.PendingAsync(Scope)).Select(x => x.Id).ToHashSet();
        var updates = result.Items.Where(m => !pending.Contains(m.Id)).ToList();
        if (updates.Count == 0) return;
        var previous = await store.MessageUpdatesAsync(Scope, ids);
        var known = previous.Select(m => m.Id).ToHashSet();
        // A receipt/edit for uncached history must not insert a disconnected old
        // row into the paginated cache or count it as a newly received message.
        updates = updates.Where(m => known.Contains(m.Id) || created.Contains(m.Id)).ToList();
        if (updates.Count == 0) return;
        await DecryptMessagesAsync(updates);
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
        Notify();
    }
}
