using System.Security.Claims;
using Bolt.Protocol;

namespace Bolt.Server;

public sealed record BoltCallAuthorizationContext(
    Guid CallId,
    SignalType Operation,
    string CallerClientId,
    ClaimsPrincipal Caller,
    string RecipientClientId,
    ClaimsPrincipal Recipient);

/// <summary>Host policy must verify tenant, conversation membership and permission to call.</summary>
public interface IBoltCallAuthorizer
{
    ValueTask<bool> AuthorizeAsync(BoltCallAuthorizationContext context, CancellationToken ct = default);
}

/// <summary>Host policy must require explicit acceptance and current conversation membership for this device.</summary>
public interface IBoltGroupCallAuthorizer
{
    ValueTask<bool> AuthorizeParticipantAsync(Guid callId, string clientId, ClaimsPrincipal participant, CancellationToken ct = default);
}
