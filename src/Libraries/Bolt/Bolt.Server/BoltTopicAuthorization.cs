using System.Security.Claims;

namespace Bolt.Server;

public enum BoltTopicOperation
{
    Subscribe,
    Publish,
    Unsubscribe,
    Ack
}

public sealed record BoltTopicAuthorizationContext(
    BoltTopicOperation Operation,
    string? Topic,
    int TopicHash,
    bool Durable,
    string? SubscriberId,
    string? ActorAccessToken,
    string ConnectionId,
    string? ClientId,
    string? ClientName,
    ClaimsPrincipal? User);

public interface IBoltTopicAuthorizer
{
    ValueTask<bool> AuthorizeAsync(BoltTopicAuthorizationContext context, CancellationToken ct = default);
}
