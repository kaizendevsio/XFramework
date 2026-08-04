using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Security;

namespace XFramework.Integration.Security;

public interface IActorIdentityProvider
{
    Task<ActorIdentityValidationResult> ValidateAsync(
        string token,
        CancellationToken ct = default);
}

public interface IServiceIdentityProvider
{
    Task<ServiceIdentityValidationResult> ValidateAsync(
        string token,
        string expectedAudience,
        CancellationToken ct = default);
}

public interface ITrustedInvocationContextAccessor : IEffectiveTenantContextAccessor
{
    TrustedInvocationContext? Current { get; }
    bool IEffectiveTenantContextAccessor.HasTrustedInvocation => Current is not null;
    Guid? IEffectiveTenantContextAccessor.EffectiveTenantId => Current?.EffectiveTenantId;
}

public interface ITrustedInvocationContextStore : ITrustedInvocationContextAccessor
{
    void Set(TrustedInvocationContext context);
}

public interface ITrustedInvocationResolver
{
    Task<TrustedInvocationResult> ResolveAsync(
        InvocationCredentials credentials,
        RequestMetadata metadata,
        InvocationAuthorizationPolicy policy,
        string expectedAudience,
        CancellationToken ct = default);
}
