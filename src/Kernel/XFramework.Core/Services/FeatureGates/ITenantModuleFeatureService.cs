using XFramework.Core.Patterns;

namespace XFramework.Core.Services.FeatureGates;

public interface ITenantModuleFeatureService
{
    Task<Result<bool>> IsEnabledAsync(
        Guid tenantId,
        string moduleKey,
        string? subFeatureKey = null,
        CancellationToken ct = default);

    Task<Result> EnsureEnabledAsync(
        Guid tenantId,
        string moduleKey,
        string? subFeatureKey = null,
        CancellationToken ct = default);

    void Invalidate(Guid tenantId, string moduleKey, string? subFeatureKey = null);
}
