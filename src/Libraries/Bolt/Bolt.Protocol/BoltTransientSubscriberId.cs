namespace Bolt.Protocol;

/// <summary>Connection-owned transient subscriptions, distinct from durable subscriber identities.</summary>
public static class BoltTransientSubscriberId
{
    public static string Create(string clientId) => $"{clientId}~{Guid.NewGuid():N}";

    public static bool IsScopedToClient(string? subscriberId, string? clientId) =>
        !string.IsNullOrEmpty(clientId) && subscriberId is not null &&
        subscriberId.Length == clientId.Length + 33 &&
        subscriberId.StartsWith(clientId, StringComparison.Ordinal) &&
        subscriberId[clientId.Length] == '~' &&
        Guid.TryParseExact(subscriberId.AsSpan(clientId.Length + 1), "N", out _);
}
