namespace XFramework.Domain.Shared.Security;

public interface IEffectiveTenantContextAccessor
{
    bool HasTrustedInvocation { get; }
    Guid? EffectiveTenantId { get; }
}
