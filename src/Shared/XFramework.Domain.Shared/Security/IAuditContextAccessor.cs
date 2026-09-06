namespace XFramework.Domain.Shared.Security;

/// <summary>
/// Exposes the trusted identity context needed to attribute database changes.
/// Implementations must only return identities established by server-side authentication.
/// </summary>
public interface IAuditContextAccessor
{
    bool HasAuditContext { get; }
    Guid? ActorCredentialId { get; }
    Guid? ActorIdentityId { get; }
    Guid? ActorTenantId { get; }
    Guid? EffectiveTenantId { get; }
    Guid? SessionId { get; }
    string? ServiceClientId { get; }
    Guid? CorrelationId { get; }
}
