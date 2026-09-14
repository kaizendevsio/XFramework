using Microsoft.JSInterop;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class ChatState
{
    private DateTime nextPresenceAt;

    private async Task PublishPresenceAsync()
    {
        if (User is null || !Online || NeedsLogin || DateTime.UtcNow < nextPresenceAt) return;
        try
        {
            if (!await js.InvokeAsync<bool>("yap.device.visible")) return;
            nextPresenceAt = DateTime.UtcNow.AddSeconds(15);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await api.PostAsync<object>("api/chat/presence", ct: timeout.Token);
        }
        catch { /* Optional presence must never block sync or surface offline error toasts. */ }
    }

    public bool IsActive(Person person) => Online && !NeedsLogin && person.ActiveUntil > DateTime.UtcNow;

    public string ConversationSubtitle(Conversation conversation)
    {
        var active = conversation.People.Count(person => person.Id != User?.CredentialId && IsActive(person));
        return conversation.Group
            ? active > 0 ? $"{conversation.Members} members · {active} active" : $"{conversation.Members} members"
            : active > 0 ? "Active now" : "Direct conversation";
    }

    public async Task SetActiveStatusAsync(Guid conversationId, bool share)
    {
        var previous = (Selected?.Id == conversationId ? Selected : Conversations.FirstOrDefault(x => x.Id == conversationId))?.ShareActiveStatus ?? !share;
        await ChangeConversationAsync(conversationId, "active-status", x => x.ShareActiveStatus = share, x => x.ShareActiveStatus = previous,
            async () => { await api.PostAsync("api/chat/thread-actions", new ThreadAction(conversationId, "active-status", share)); await SynchronizeAsync(); });
    }
}
