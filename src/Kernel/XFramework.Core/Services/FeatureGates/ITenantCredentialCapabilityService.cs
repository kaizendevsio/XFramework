using XFramework.Core.Patterns;

namespace XFramework.Core.Services.FeatureGates;

public interface ITenantCredentialCapabilityService
{
    Task<Result<bool>> IsAllowedAsync(
        Guid tenantId,
        Guid credentialId,
        string moduleKey,
        string? subFeatureKey,
        string capabilityKey,
        CancellationToken ct = default);

    Task<Result> EnsureAllowedAsync(
        Guid tenantId,
        Guid credentialId,
        string moduleKey,
        string? subFeatureKey,
        string capabilityKey,
        CancellationToken ct = default);
}
