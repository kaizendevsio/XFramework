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

    /// <summary>One line of a message's status detail. <c>At</c> is null when the step has not happened.
    /// No text is formatted here: the published client has no timezone data, so the instant travels
    /// and the browser turns it into words.</summary>
    public sealed record StatusRow(string Key, string Who, string State, DateTime? At, bool Read);

    /// <summary>Sent/delivered/read for one of the caller's own messages. A direct chat has one
    /// counterpart and reads as two labelled lines; a group names people - read first and most recent
    /// first, then merely delivered, then whoever it has not reached. The unreached come from the
    /// roster already on the device, so naming them costs no request and no bytes on the wire.
    /// A null detail (someone else's message, or offline) has nothing to add beyond "sent".</summary>
    public static List<StatusRow> Status(MessageReceiptDetail? detail, bool group, IEnumerable<Person> roster, Guid? self)
    {
        if (detail is null) return [];
        var entries = detail.Entries.OrderByDescending(x => detail.ReadReceipts ? x.ReadAt : null)
            .ThenByDescending(x => x.DeliveredAt).ToList();
        if (!group)
        {
            if (entries.FirstOrDefault() is not { } only) return [new("delivered", "Delivered", "Not yet", null, false)];
            List<StatusRow> lines = [new("delivered", "Delivered", "", only.DeliveredAt, false)];
            if (detail.ReadReceipts && only.ReadAt is { } read) lines.Add(new("read", "Read", "", read, true));
            return lines;
        }
        var rows = entries.Select(entry => detail.ReadReceipts && entry.ReadAt is { } read
            ? new StatusRow($"{entry.Person.Id}", entry.Person.Name, "Read", read, true)
            : new StatusRow($"{entry.Person.Id}", entry.Person.Name, "Delivered", entry.DeliveredAt, false)).ToList();
        rows.AddRange(roster.Where(person => person.Id != self && detail.Entries.All(x => x.Person.Id != person.Id))
            .OrderBy(person => person.Name, StringComparer.Ordinal)
            .Select(person => new StatusRow($"{person.Id}", person.Name, "Not delivered", null, false)));
        return rows;
    }

    public static List<Person> Readers(ChatMessage message, List<ChatMessage> messages) => message.LatestReaders
        .Where(person => !messages.Any(later => later.CreatedAt > message.CreatedAt
            && later.LatestReaders.Any(reader => reader.Id == person.Id))).ToList();
}
