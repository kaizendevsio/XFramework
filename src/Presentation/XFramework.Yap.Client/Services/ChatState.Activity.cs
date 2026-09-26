using System.Collections.Concurrent;
using Yap.Contracts;

namespace Yap.Client.Services;

/// <summary>
/// Attachment activity: "Sending a photo…" in the conversation header while someone attaches
/// something. It is the typing channel with a kind and a count - the same transport, the same
/// 3 s refresh and 6 s expiry, and the same per-conversation typing setting in both directions.
/// Only the kind and how many ever travel; never a file name, size or content.
/// </summary>
public sealed partial class ChatState
{
    /// <summary>How often a live activity is re-announced; typing is throttled to the same interval.</summary>
    internal static TimeSpan ActivityRefresh = TimeSpan.FromSeconds(3);
    /// <summary>How long a received activity lasts without a refresh; the same as typing.</summary>
    internal static TimeSpan ActivityLifetime = TimeSpan.FromSeconds(6);
    /// <summary>A staged attachment nobody sends stops being announced after this long.</summary>
    internal static TimeSpan ActivityLimit = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, (ChatActivity Kind, int Count, DateTime Until)> activities = new();
    private (Guid Thread, ChatActivity Kind, int Count)? outgoingActivity;
    private Guid? activityMessage;
    private long activityVersion;

    /// <summary>The outgoing activity, for tests and diagnostics.</summary>
    public (Guid Thread, ChatActivity Kind, int Count)? OutgoingActivity => outgoingActivity;

    public static ChatActivity ActivityFor(string? contentType) => (contentType ?? "").Split('/')[0] switch
    {
        "image" => ChatActivity.Photo,
        "video" => ChatActivity.Video,
        _ => ChatActivity.File
    };

    /// <summary>"sending 3 photos…", lower-case so a group can put a name in front of it.</summary>
    public static string ActivityPhrase(ChatActivity kind, int count)
    {
        if (kind == ChatActivity.Recording) return "recording a voice message…";
        var (one, many) = kind switch
        {
            ChatActivity.Photo => ("a photo", "photos"),
            ChatActivity.Video => ("a video", "videos"),
            ChatActivity.VoiceMessage => ("a voice message", "voice messages"),
            _ => ("a file", "files")
        };
        return count > 1 ? $"sending {count} {many}…" : $"sending {one}…";
    }

    /// <summary>What replaces the header's status line while someone is attaching, or null.</summary>
    public string? ActivityText(Conversation conversation)
    {
        if (Selected?.Id != conversation.Id || !conversation.Allows(ChatFeature.Typing) || !Online || NeedsLogin) return null;
        var now = DateTime.UtcNow;
        var live = activities.Where(x => x.Value.Until > now && x.Key != User?.CredentialId)
            .OrderByDescending(x => x.Value.Until).ToList();
        if (live.Count == 0) return null;
        var (who, (kind, count, _)) = (live[0].Key, live[0].Value);
        var phrase = ActivityPhrase(kind, count);
        if (!conversation.Group) return char.ToUpperInvariant(phrase[0]) + phrase[1..];
        var name = FirstName(conversation.People.FirstOrDefault(x => x.Id == who)?.Name);
        return live.Count switch
        {
            1 => $"{name} is {phrase}",
            2 => $"{name} and 1 other are sending…",
            _ => $"{name} and {live.Count - 1} others are sending…"
        };
    }

    private static string FirstName(string? name) =>
        name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Someone";

    private void ActivityChanged(Guid thread, Guid credential, bool active, ChatActivity kind, int count)
    {
        if (!active) { activities.TryRemove(credential, out _); return; }
        var until = DateTime.UtcNow.Add(ActivityLifetime);
        activities[credential] = (kind, kind == ChatActivity.Recording ? 0 : Math.Clamp(count, 1, 99), until);
        _ = ExpireActivityAsync(thread, credential);
    }

    private async Task ExpireActivityAsync(Guid thread, Guid credential)
    {
        try { await Task.Delay(ActivityLifetime, lifetime.Token); }
        catch (OperationCanceledException) { return; }
        if (Selected?.Id == thread && activities.TryGetValue(credential, out var current) && current.Until <= DateTime.UtcNow)
        { ((ICollection<KeyValuePair<Guid, (ChatActivity, int, DateTime)>>)activities).Remove(new(credential, current)); Notify(); }
    }

    /// <summary>Forgets everyone's typing and attachment activity (conversation change, offline, sign-out).</summary>
    private void ClearTyping() { typing.Clear(); activities.Clear(); }

    /// <summary>
    /// Announces an attachment being prepared in <paramref name="thread"/> and keeps announcing it
    /// until <see cref="EndActivityAsync"/>, a send completes or fails, or <see cref="ActivityLimit"/>.
    /// Repeating the same announcement is free: the refresh loop is already carrying it.
    /// </summary>
    public async Task BeginActivityAsync(Guid thread, ChatActivity kind, int count = 1)
    {
        if (kind == ChatActivity.Typing || !Enum.IsDefined(kind)) return;
        count = kind == ChatActivity.Recording ? 0 : Math.Clamp(count, 1, 99);
        if (outgoingActivity == (thread, kind, count)) return;
        if (outgoingActivity is { } previous && previous.Thread != thread) await EndActivityAsync(previous.Thread);
        var version = Interlocked.Increment(ref activityVersion);
        outgoingActivity = (thread, kind, count);
        activityMessage = null;
        await PostActivityAsync(thread, kind, true, count);
        _ = KeepActivityAsync(version);
    }

    /// <summary>Withdraws the announcement for <paramref name="thread"/> (any thread when null).</summary>
    public async Task EndActivityAsync(Guid? thread = null)
    {
        if (outgoingActivity is not { } current || thread is { } only && current.Thread != only) return;
        Interlocked.Increment(ref activityVersion);
        outgoingActivity = null; activityMessage = null;
        await PostActivityAsync(current.Thread, current.Kind, false, current.Count);
    }

    /// <summary>The queued message that carries the announced attachment; its upload ending ends the announcement.</summary>
    private void BindActivity(Guid thread, Guid message)
    {
        if (outgoingActivity?.Thread == thread) activityMessage = message;
    }

    private Task FinishActivityAsync(Guid message) =>
        activityMessage == message && outgoingActivity is { } current ? EndActivityAsync(current.Thread) : Task.CompletedTask;

    private async Task KeepActivityAsync(long version)
    {
        var deadline = DateTime.UtcNow.Add(ActivityLimit);
        while (true)
        {
            try { await Task.Delay(ActivityRefresh, lifetime.Token); }
            catch (OperationCanceledException) { return; }
            if (version != Interlocked.Read(ref activityVersion) || outgoingActivity is not { } current) return;
            if (DateTime.UtcNow >= deadline) { await EndActivityAsync(current.Thread); return; }
            await PostActivityAsync(current.Thread, current.Kind, true, current.Count);
        }
    }

    private bool TypingAllowed(Guid thread) =>
        (Selected?.Id == thread ? Selected : Conversations.FirstOrDefault(x => x.Id == thread))?.Allows(ChatFeature.Typing) != false;

    private async Task PostActivityAsync(Guid thread, ChatActivity kind, bool active, int count)
    {
        await typingPublish.WaitAsync();
        try
        {
            // Typing off in this conversation means no activity leaves the device either.
            if (!Online || NeedsLogin || User is null || !TypingAllowed(thread)) return;
            try { await api.PostAsync("api/chat/thread-actions", new ThreadAction(thread, "typing", active, kind, count)); }
            catch (Exception) { /* Activity is transient like typing; the upload itself is independent. */ }
        }
        finally { typingPublish.Release(); }
    }
}
