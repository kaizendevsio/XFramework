namespace XFramework.Integration.Security;

internal sealed class TrustedInvocationContextAccessor : ITrustedInvocationContextStore
{
    public TrustedInvocationContext? Current { get; private set; }
    public bool HasTrustedInvocation => Current is not null;
    public Guid? EffectiveTenantId => Current?.EffectiveTenantId;

    public void Set(TrustedInvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Current is null)
        {
            Current = context;
            return;
        }

        if (ContextsMatch(Current, context))
            return;

        throw new InvalidOperationException(
            "A trusted invocation context has already been established for this scope.");
    }

    private static bool ContextsMatch(TrustedInvocationContext current, TrustedInvocationContext next) =>
        ActorsMatch(current.Actor, next.Actor) &&
        ServicesMatch(current.Service, next.Service) &&
        current.EffectiveTenantId == next.EffectiveTenantId &&
        current.RequestedTargetTenantId == next.RequestedTargetTenantId &&
        current.CorrelationId == next.CorrelationId;

    private static bool ActorsMatch(TrustedActorIdentity? current, TrustedActorIdentity? next) =>
        current is null
            ? next is null
            : next is not null &&
              current.CredentialId == next.CredentialId &&
              current.IdentityId == next.IdentityId &&
              current.TenantId == next.TenantId &&
              current.SessionId == next.SessionId &&
              current.Roles.SetEquals(next.Roles) &&
              current.Capabilities.SetEquals(next.Capabilities) &&
              string.Equals(current.GenerationId, next.GenerationId, StringComparison.Ordinal) &&
              current.ExpiresAtUtc == next.ExpiresAtUtc;

    private static bool ServicesMatch(TrustedServiceIdentity? current, TrustedServiceIdentity? next) =>
        current is null
            ? next is null
            : next is not null &&
              string.Equals(current.ClientId, next.ClientId, StringComparison.OrdinalIgnoreCase) &&
              string.Equals(current.Audience, next.Audience, StringComparison.OrdinalIgnoreCase) &&
              current.Scopes.SetEquals(next.Scopes) &&
              string.Equals(current.GenerationId, next.GenerationId, StringComparison.Ordinal);
}
