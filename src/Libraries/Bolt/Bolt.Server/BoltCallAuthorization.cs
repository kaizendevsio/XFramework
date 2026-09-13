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
