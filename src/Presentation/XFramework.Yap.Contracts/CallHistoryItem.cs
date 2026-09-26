namespace Yap.Contracts;

public sealed record CallHistoryItem(ChatMessage Message, string ConversationName, bool Group, string? AvatarUrl = null);

/// <summary>Who placed a call and whether anyone picked up. The summary's sender is the caller.</summary>
public enum CallDirection { Outgoing, Incoming, Missed, Unanswered }

public static class CallHistory
{
    private const string Separator = " · ";

    // Old summaries have only the server-generated label. Keep these readable without a data migration.
    public static bool IsVideo(ChatMessage message) => message.IsCallSummary &&
        (message.Text.StartsWith("Video call", StringComparison.Ordinal) || message.Text == "Missed video call");
    public static string Label(ChatMessage message) => message.Mine && message.Text.StartsWith("Missed ", StringComparison.Ordinal)
        ? "Unanswered " + message.Text[7..] : message.Text;

    /// <summary>Missed is only ever someone else's call to me; my own unanswered call is not an alarm.</summary>
    public static CallDirection Direction(ChatMessage message) => message.Text.StartsWith("Missed ", StringComparison.Ordinal)
        ? message.Mine ? CallDirection.Unanswered : CallDirection.Missed
        : message.Mine ? CallDirection.Outgoing : CallDirection.Incoming;

    /// <summary>The server's "m:ss" for a connected call, or null for one nobody answered.</summary>
    public static string? Duration(ChatMessage message)
    {
        var at = message.Text.IndexOf(Separator, StringComparison.Ordinal);
        return at < 0 ? null : message.Text[(at + Separator.Length)..];
    }

    /// <summary>"Voice call" / "Video call": the kind without the duration or the outcome.</summary>
    public static string Kind(ChatMessage message) => IsVideo(message) ? "Video call" : "Voice call";

    /// <summary>A detail line: "Incoming voice call", "Missed video call", "Unanswered voice call".</summary>
    public static string Describe(ChatMessage message) => Direction(message) switch
    {
        CallDirection.Outgoing => "Outgoing " + Lower(Kind(message)),
        CallDirection.Incoming => "Incoming " + Lower(Kind(message)),
        CallDirection.Missed => "Missed " + Lower(Kind(message)),
        _ => "Unanswered " + Lower(Kind(message))
    };

    /// <summary>"5:09" read aloud as "5 minutes 9 seconds" - a screen reader says "five colon oh nine".</summary>
    public static string? SpokenDuration(ChatMessage message)
    {
        if (Duration(message) is not { } text) return null;
        var parts = text.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var minutes) || !int.TryParse(parts[1], out var seconds)) return text;
        var words = new List<string>(2);
        if (minutes > 0) words.Add(minutes == 1 ? "1 minute" : $"{minutes} minutes");
        if (seconds > 0 || minutes == 0) words.Add(seconds == 1 ? "1 second" : $"{seconds} seconds");
        return string.Join(' ', words);
    }

    private static string Lower(string kind) => kind.ToLowerInvariant();
}
