using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class ChatState
{
    private async Task ResolvePreviewsAsync(List<Conversation> conversations)
    {
        if (User is null) return;
        var pending = new List<ChatMessage>();
        foreach (var conversation in conversations)
        {
            if (conversation.LastMessage is not { } message) continue;
            var cached = Conversations.FirstOrDefault(c => c.Id == conversation.Id)?.LastMessage;
            if (cached is { EncryptionLocked: false } && cached.Id == message.Id &&
                cached.EncryptedEnvelope == message.EncryptedEnvelope && !message.EncryptionPending)
                conversation.LastMessage = message = cached;
            else pending.Add(message);
        }
        await Encryption.DecryptAsync(User, pending);
        foreach (var conversation in conversations)
            if (conversation.LastMessage is { } message) conversation.Preview = MessagePreview(message);
        // The summary carries a readable message, so every conversation can be
        // acknowledged here — not only the one the reader happens to have open.
        await AcknowledgeInboxAsync(conversations);
    }

    internal static string MessagePreview(ChatMessage message)
    {
        if (message.EncryptionPending) return "Waiting for secure delivery";
        if (message.EncryptionLocked) return "Unlock messages in Settings";
        var text = message.Text.Trim();
        if (text.Length > 0 && !message.Attachments.Any(f => f.Name == text))
            return text.Length > 100 ? text[..100] + "…" : text;
        if (message.Attachments.Count == 0) return "Message";
        var type = ChatMedia.ContentType(message.Attachments[0].ContentType, message.Attachments[0].Name);
        var name = type.StartsWith("image/") ? "Photo" : type.StartsWith("video/") ? "Video" : type.StartsWith("audio/") ? "Voice message" : "Attachment";
        return message.Attachments.Count == 1 ? name : $"{message.Attachments.Count} attachments";
    }
}
