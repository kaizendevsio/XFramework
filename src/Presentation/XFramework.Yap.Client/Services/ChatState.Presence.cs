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
        if (conversation.Group) return active > 0 ? $"{active} active now" : $"{conversation.Members} members";
        if (active > 0) return "Active now";
        var last = conversation.People.FirstOrDefault(person => person.Id != User?.CredentialId)?.LastActiveAt;
        if (last is null || !Online || NeedsLogin) return "";
        var age = DateTime.UtcNow - last.Value;
        if (age < TimeSpan.FromMinutes(1)) return "Last seen just now";
        if (age < TimeSpan.FromHours(1)) return $"Last seen {(int)age.TotalMinutes}m ago";
        return age < TimeSpan.FromDays(1) ? $"Last seen {(int)age.TotalHours}h ago" : "";
    }

    public async Task SetActiveStatusAsync(Guid conversationId, bool share)
    {
        var previous = (Selected?.Id == conversationId ? Selected : Conversations.FirstOrDefault(x => x.Id == conversationId))?.ShareActiveStatus ?? !share;
        await ChangeConversationAsync(conversationId, "active-status", x => x.ShareActiveStatus = share, x => x.ShareActiveStatus = previous,
            async () => { await api.PostAsync("api/chat/thread-actions", new ThreadAction(conversationId, "active-status", share)); await SynchronizeAsync(); });
    }
}
