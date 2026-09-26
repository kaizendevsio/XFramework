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

    // Presence dots. A cached heartbeat means nothing while this device is offline or signed out,
    // and neither your own avatar nor a group's ever carries one.
    public PresenceStatus PresenceOf(Person person) =>
        !Online || NeedsLogin || person.Id == User?.CredentialId ? PresenceStatus.Offline
        : PresenceDot.Of(person.ActiveUntil, person.LastActiveAt, DateTime.UtcNow);

    public PresenceStatus PresenceOf(Conversation conversation) =>
        !Online || NeedsLogin || conversation.Group || conversation.PeerId is not { } peer || peer == User?.CredentialId ? PresenceStatus.Offline
        : PresenceDot.Of(conversation.PeerActiveUntil, conversation.PeerLastActiveAt, DateTime.UtcNow);

    /// <summary>For a row that names a conversation (a call, say) rather than holding it.</summary>
    public PresenceStatus PresenceOfConversation(Guid conversationId) =>
        (Selected?.Id == conversationId ? Selected : Conversations.FirstOrDefault(x => x.Id == conversationId)) is { } conversation
            ? PresenceOf(conversation) : PresenceStatus.Offline;

    /// <summary>For a person picker. Active status is shared per conversation, so only a direct chat
    /// with this person can say it; without one there is no dot rather than a guess.</summary>
    public PresenceStatus PresenceOfPerson(Guid credentialId) =>
        Conversations.FirstOrDefault(x => !x.Group && !x.Removed && x.PeerId == credentialId) is { } direct
            ? PresenceOf(Selected?.Id == direct.Id ? Selected : direct) : PresenceStatus.Offline;

    public string ConversationSubtitle(Conversation conversation)
    {
        // An attachment in progress takes the status line's place while it lasts.
        if (ActivityText(conversation) is { } activity) return activity;
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
