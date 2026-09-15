using System.Text.Json;
using Communications.Domain.Shared.Contracts.Realtime;
using Yap.Contracts;

namespace Yap.Services;

public static class YapRealtime
{
    // Forward only routing metadata from an already authorized user subscription.
    // Message content and receipts are fetched through the normal visibility checks.
    public static string Frame(CommunicationsRealtimeEvent update)
    {
        if (update.ThreadId is not { } thread || update.EventType is not ("MessageCreated" or "MessageEdited" or "ReactionCreated" or "ReactionDeleted" or "MessagesRead" or "MessagesDelivered"))
            return "data: refresh\n\n";
        try
        {
            using var payload = JsonDocument.Parse(update.PayloadJson);
            var ids = new List<Guid>();
            foreach (var property in payload.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("messageId", StringComparison.OrdinalIgnoreCase) && property.Value.TryGetGuid(out var id)) ids.Add(id);
                if (property.Name.Equals("messageIds", StringComparison.OrdinalIgnoreCase))
                    foreach (var value in property.Value.EnumerateArray())
                    {
                        if (!value.TryGetGuid(out var item) || ids.Count >= 50) return "data: refresh\n\n";
                        ids.Add(item);
                    }
            }
            if (ids.Count is 0 or > 50 || ids.Contains(Guid.Empty)) return "data: refresh\n\n";
            return $"data: {JsonSerializer.Serialize(new ChatUpdateHint(thread, update.EventType, update.ActorCredentialId, ids.Distinct().ToList()))}\n\n";
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        { return "data: refresh\n\n"; }
    }
}
