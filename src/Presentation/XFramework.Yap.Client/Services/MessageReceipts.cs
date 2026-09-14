using Yap.Contracts;

namespace Yap.Client.Services;

public static class MessageReceipts
{
    public static bool IsSending(ChatMessage message) => message.Delivery is "Queued" or "Sending"
        or "Sending · waiting for recipient setup";

    public static bool ShowStatus(ChatMessage message, bool latest, DateTime now) => message.Mine
        && (IsSending(message) ? now - message.CreatedAt >= TimeSpan.FromSeconds(5)
            : message.Delivery != "Sent" || latest);

    public static bool IsLatest(ChatMessage message, List<ChatMessage> messages) => message.Mine
        && messages.Where(x => x.Mine && (x.IsLatestOwnMessage || IsSending(x)))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).LastOrDefault()?.Id == message.Id;

    public static List<Person> Readers(ChatMessage message, List<ChatMessage> messages) => message.LatestReaders
        .Where(person => !messages.Any(later => later.CreatedAt > message.CreatedAt
            && later.LatestReaders.Any(reader => reader.Id == person.Id))).ToList();
}
