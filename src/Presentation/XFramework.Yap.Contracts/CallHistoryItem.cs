namespace Yap.Contracts;

public sealed record CallHistoryItem(ChatMessage Message, string ConversationName, bool Group, string? AvatarUrl = null);

public static class CallHistory
{
    // Old summaries have only the server-generated label. Keep these readable without a data migration.
    public static bool IsVideo(ChatMessage message) => message.IsCallSummary &&
        (message.Text.StartsWith("Video call", StringComparison.Ordinal) || message.Text == "Missed video call");
    public static string Label(ChatMessage message) => message.Mine && message.Text.StartsWith("Missed ", StringComparison.Ordinal)
        ? "Unanswered " + message.Text[7..] : message.Text;
}
