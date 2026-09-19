using Yap.Contracts;

namespace Yap.Client.Services;

/// <summary>
/// The two per-message details a person can ask for: who reacted, and when their own message
/// reached each member. Both are fetched for one message at a time and never cached offline -
/// a message page carries counts, and counts are what the cached history is allowed to age into.
/// </summary>
public sealed partial class ChatState
{
    /// <summary>Who reacted with what. Empty offline: a reaction badge is still honest without it.</summary>
    public async Task<List<MessageReactor>> ReactorsAsync(ChatMessage message, CancellationToken ct = default)
    {
        if (!Online || NeedsLogin) return [];
        try { return await api.GetAsync<List<MessageReactor>>(Detail(message, "reactions"), ct); }
        catch (Exception ex) { Report(ex); return []; }
    }

    /// <summary>Delivery and read times for the caller's own message. Null when the host has nothing
    /// to tell: offline, or a message that is not the caller's, which the host refuses by design.</summary>
    public async Task<MessageReceiptDetail?> ReceiptsAsync(ChatMessage message, CancellationToken ct = default)
    {
        if (!message.Mine || !Online || NeedsLogin) return null;
        try { return await api.GetAsync<MessageReceiptDetail>(Detail(message, "receipts"), ct); }
        catch (Exception ex) { Report(ex); return null; }
    }

    private static string Detail(ChatMessage message, string kind) =>
        $"api/chat/conversations/{message.ThreadId}/messages/{message.Id}/{kind}";
}
