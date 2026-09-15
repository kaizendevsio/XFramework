using System.Text.Json;

namespace Yap.Contracts;

public sealed record ChatSocketRequest(string Operation, JsonElement Body);
public sealed record ChatSocketResponse(int Status, JsonElement? Body = null);
public sealed record ChatSocketTicket(string Url, string ClientId);
public sealed record ChatSocketEvent(Guid EventId, long Sequence, string Kind,
    Guid? ThreadId = null, Guid? ActorId = null, List<Guid>? MessageIds = null,
    List<ChatMessage>? Messages = null, JsonElement? Body = null);
