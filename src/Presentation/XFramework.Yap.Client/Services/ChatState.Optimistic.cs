using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class ChatState
{
    // These overlays are display-only. The durable outbox still precedes network sends.
    private long messageMutationVersion;
    private readonly Dictionary<Guid, (string Scope, ChatMessage Message)> stagingMessages = [];
    private readonly Dictionary<Guid, (string Scope, Action<ChatMessage> Apply, bool Delete)> optimisticMessages = [];
    private readonly Dictionary<(Guid Id, string Field), (string Scope, Action<Conversation> Apply)> optimisticConversations = [];
    public bool SavingMessages => stagingMessages.Values.Any(x => x.Scope == Scope);

    private void ApplyOptimisticConversations()
    {
        foreach (var conversation in Conversations.Concat(Selected is null ? [] : new[] { Selected }).Distinct())
            foreach (var change in optimisticConversations.Where(x => x.Key.Id == conversation.Id && x.Value.Scope == Scope))
                change.Value.Apply(conversation);
    }

    private async Task ChangeConversationAsync(Guid id, string field, Action<Conversation> apply, Action<Conversation> undo, Func<Task> save)
    {
        var key = (id, field);
        if (optimisticConversations.ContainsKey(key)) return;
        var scope = Scope;
        optimisticConversations[key] = (scope, apply);
        ApplyOptimisticConversations(); Notify();
        try { await save(); }
        catch
        {
            if (Scope == scope)
                foreach (var conversation in Conversations.Concat(Selected is null ? [] : new[] { Selected }).Where(x => x.Id == id).Distinct()) undo(conversation);
            throw;
        }
        finally { optimisticConversations.Remove(key); Notify(); }
    }

    private void MergeStaging(Conversation conversation)
    {
        if (historyOffset != 0) return;
        foreach (var staged in stagingMessages.Values.Where(x => x.Scope == Scope && x.Message.ThreadId == conversation.Id))
        {
            // A staged thread reply shows under its parent, never in the timeline it was sent from.
            var target = staged.Message.IsThreadReply && staged.Message.ParentId is { } parent
                ? conversation.Messages.FirstOrDefault(x => x.Id == parent)?.Replies : conversation.Messages;
            if (target is not null && target.All(x => x.Id != staged.Message.Id)) target.Add(staged.Message);
        }
        conversation.Messages = conversation.Messages.OrderBy(x => x.CreatedAt).TakeLast(HistoryWindowSize).ToList();
    }

    private void ApplyOptimisticMessages(Conversation conversation)
    {
        foreach (var message in conversation.Messages.Concat(conversation.Messages.SelectMany(x => x.Replies)))
            if (optimisticMessages.TryGetValue(message.Id, out var change) && change.Scope == Scope) change.Apply(message);
        bool Hidden(ChatMessage message) => optimisticMessages.TryGetValue(message.Id, out var change) && change.Scope == Scope && change.Delete;
        foreach (var message in conversation.Messages) message.Replies.RemoveAll(Hidden);
        conversation.Messages.RemoveAll(Hidden);
    }

    private static void SetReaction(ChatMessage message, string emoji, bool present, Guid reactionId)
    {
        var wasPresent = message.MyReactionIds.ContainsKey(emoji);
        if (wasPresent == present) return;
        var count = message.Reactions.GetValueOrDefault(emoji) + (present ? 1 : -1);
        if (count > 0) message.Reactions[emoji] = count; else message.Reactions.Remove(emoji);
        if (present) message.MyReactionIds[emoji] = reactionId; else message.MyReactionIds.Remove(emoji);
    }

    private Action BeginOptimisticMessage(ChatMessage message, string action, string? text, string? emoji)
    {
        var scope = Scope;
        var previousText = message.Text; var pinned = message.Pinned; var saved = message.Saved;
        var reacted = emoji is not null && message.MyReactionIds.ContainsKey(emoji);
        var reactionId = emoji is null ? Guid.Empty : message.MyReactionIds.GetValueOrDefault(emoji);
        void Apply(ChatMessage target)
        {
            switch (action)
            {
                case "edit": target.Text = text ?? ""; break;
                case "pin": target.Pinned = true; break;
                case "unpin": target.Pinned = false; break;
                case "save": target.Saved = true; break;
                case "unsave": target.Saved = false; break;
                case "react" when emoji is not null: SetReaction(target, emoji, true, Guid.Empty); break;
                case "unreact" when emoji is not null: SetReaction(target, emoji, false, Guid.Empty); break;
            }
        }
        optimisticMessages[message.Id] = (scope, Apply, action == "delete");
        messageMutationVersion++;
        Apply(message);
        if (Selected?.Id == message.ThreadId) ApplyOptimisticMessages(Selected);
        Notify();
        return () =>
        {
            if (Scope != scope || Selected?.Id != message.ThreadId) return;
            var current = Selected.Messages.Concat(Selected.Messages.SelectMany(x => x.Replies)).FirstOrDefault(x => x.Id == message.Id) ?? message;
            switch (action)
            {
                case "edit": current.Text = previousText; break;
                case "pin" or "unpin": current.Pinned = pinned; break;
                case "save" or "unsave": current.Saved = saved; break;
                case "react" or "unreact" when emoji is not null: SetReaction(current, emoji, reacted, reactionId); break;
                case "delete":
                    if (message.IsThreadReply && message.ParentId is { } parentId && Selected.Messages.FirstOrDefault(x => x.Id == parentId) is { } parent)
                        parent.Replies = parent.Replies.Append(message).DistinctBy(x => x.Id).OrderBy(x => x.CreatedAt).TakeLast(HistoryWindowSize).ToList();
                    else if (Selected.Messages.All(x => x.Id != message.Id))
                    {
                        Selected.Messages = Selected.Messages.Append(message).OrderBy(x => x.CreatedAt).TakeLast(HistoryWindowSize).ToList();
                        ComposeReplies(Selected.Messages);
                    }
                    break;
            }
        };
    }
}
