using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class ChatState
{
    public const int SavedPageSize = 30;

    /// <summary>
    /// True once the account-bound session is established. A cold start restores the signed-in
    /// user from the device before the background sync binds the account, and an account-scoped
    /// read issued in that window is rejected; pages wait for this instead of racing it.
    /// </summary>
    public bool SessionReady => User is not null && !NeedsLogin && api.Account == Scope;

    /// <summary>
    /// The saved list the Saved tab renders. The server holds the authoritative bookmarks;
    /// rows arrive as ciphertext, so they are decrypted here before anything is shown.
    /// </summary>
    public async Task<ChatPage<SavedMessage>> SavedMessagesAsync(int page = 0)
    {
        if (User is null) return new([], 0);
        if (!Online || !SessionReady) return await CachedSavedMessagesAsync(page);
        try
        {
            var result = await api.GetAsync<ChatPage<SavedMessage>>($"api/chat/saved?page={page}");
            var messages = result.Items.Select(x => x.Message).ToList();
            await DecryptMessagesAsync(messages);
            // Caching the page keeps the list readable offline and lets a tap land on a
            // conversation window that already holds the message.
            await store.SaveMessagesAsync(Scope, messages);
            return result;
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            SetOffline(); Notify();
            return await CachedSavedMessagesAsync(page);
        }
    }

    /// <summary>
    /// The device copy. It orders by when the message was sent rather than when it was saved,
    /// because the save time lives only on the server; ordering is the one thing offline loses.
    /// </summary>
    private async Task<ChatPage<SavedMessage>> CachedSavedMessagesAsync(int page)
    {
        var total = await store.SavedMessageCountAsync(Scope);
        var messages = await store.SavedMessagesAsync(Scope, SavedPageSize, page * SavedPageSize);
        return new(messages.Select(Describe).ToList(), total);
    }

    private SavedMessage Describe(ChatMessage message)
    {
        var conversation = Conversations.FirstOrDefault(x => x.Id == message.ThreadId);
        return new SavedMessage(message, conversation?.Name ?? "Conversation", conversation?.Group == true,
            message.CreatedAt, conversation?.AvatarUrl);
    }

    /// <summary>Removes a bookmark from the saved list, and from the conversation if it is open.</summary>
    public async Task<bool> UnsaveAsync(ChatMessage message)
    {
        if (!Online || !SessionReady) { Error = "Connect and sign in to change a saved message."; Notify(); return false; }
        if (!activeActions.Add(message.Id)) return false;
        try { await api.PostAsync("api/chat/message-actions", new MessageAction(message.ThreadId, message.Id, "unsave")); }
        // Another device already removed it; the row should still leave this list.
        catch (ChatApiException ex) when (ex.Status is 404 or 409) { }
        catch (Exception ex) { Report(ex); return false; }
        finally { activeActions.Remove(message.Id); }
        message.Saved = false;
        await store.SetSavedAsync(Scope, message.Id, false);
        if (Selected is { } open)
            foreach (var shown in open.Messages.Concat(open.Messages.SelectMany(x => x.Replies)).Where(x => x.Id == message.Id))
                shown.Saved = false;
        Notify();
        return true;
    }
}
