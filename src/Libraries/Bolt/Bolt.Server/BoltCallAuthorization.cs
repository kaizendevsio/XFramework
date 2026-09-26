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

/// <summary>Result of re-checking a participant that is already in a group call.</summary>
public enum BoltGroupAuthorizationDecision
{
    /// <summary>The participant is still authorized.</summary>
    Allowed,
    /// <summary>A definitive refusal: membership removed, device revoked, session ended. The participant is removed.</summary>
    Denied,
    /// <summary>
    /// The policy could not be evaluated right now (backend unreachable, timeout, transient error).
    /// The participant keeps its seat for at most <see cref="BoltServerOptions.GroupAuthorizationGraceSeconds"/>
    /// since its last successful check.
    /// </summary>
    Unavailable
}

/// <summary>Host policy must require explicit acceptance and current conversation membership for this device.</summary>
public interface IBoltGroupCallAuthorizer
{
    /// <summary>Admission check. Anything but an explicit true refuses the join.</summary>
    ValueTask<bool> AuthorizeParticipantAsync(Guid callId, string clientId, ClaimsPrincipal participant, CancellationToken ct = default);

    /// <summary>
    /// Periodic re-check of an admitted participant. Implementations should return
    /// <see cref="BoltGroupAuthorizationDecision.Unavailable"/> only when the decision could not be made,
    /// never for a refusal. The default maps the admission check; an exception is treated as unavailable.
    /// </summary>
    async ValueTask<BoltGroupAuthorizationDecision> RenewParticipantAsync(
        Guid callId, string clientId, ClaimsPrincipal participant, CancellationToken ct = default) =>
        await AuthorizeParticipantAsync(callId, clientId, participant, ct)
            ? BoltGroupAuthorizationDecision.Allowed
            : BoltGroupAuthorizationDecision.Denied;
}
