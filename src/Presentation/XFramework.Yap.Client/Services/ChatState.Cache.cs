namespace Yap.Client.Services;

public sealed partial class ChatState
{
    private async Task ReloadConversationsAsync()
    {
        var scope = Scope;
        // Keep the cache read and UI assignment in the same short critical section
        // as a push. Otherwise an older read can replace the list while a push is
        // saving changes through a reference to the previous conversation object.
        await messageChanges.WaitAsync(lifetime.Token);
        try
        {
            if (Scope != scope) return;
            var conversations = await store.ConversationsAsync(scope);
            if (Scope == scope) Conversations = conversations;
        }
        finally { messageChanges.Release(); }
    }
}
