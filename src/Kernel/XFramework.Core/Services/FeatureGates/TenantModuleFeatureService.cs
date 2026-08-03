using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.Patterns;

namespace XFramework.Core.Services.FeatureGates;

public sealed class TenantModuleFeatureService(
    DbContext dbContext,
    ILogger<TenantModuleFeatureService> logger) : ITenantModuleFeatureService
{
    public async Task<Result<bool>> IsEnabledAsync(
        Guid tenantId,
        string moduleKey,
        string? subFeatureKey = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return Result<bool>.Failure("TenantId is required.", 400);

        var (normalizedModuleKey, normalizedSubFeatureKey) =
            TenantModuleFeatureKeys.Normalize(moduleKey, subFeatureKey);

        if (string.IsNullOrWhiteSpace(normalizedModuleKey))
            return Result<bool>.Failure("Module key is required.", 400);

        var dbEnabled = await dbContext.Set<TenantModuleFeature>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(feature =>
                feature.TenantId == tenantId &&
                !feature.IsDeleted &&
                feature.ModuleKey == normalizedModuleKey &&
                feature.SubFeatureKey == normalizedSubFeatureKey)
            .Select(feature => (bool?)feature.IsEnabled)
            .FirstOrDefaultAsync(ct);

        var enabled = dbEnabled ?? false;

        logger.LogDebug(
            "Tenant module feature resolved for feature {FeatureKey}: {Enabled}",
            TenantModuleFeatureKeys.Combine(normalizedModuleKey, normalizedSubFeatureKey),
            enabled);

        return Result<bool>.Success(enabled);
    }

    public async Task<Result> EnsureEnabledAsync(
        Guid tenantId,
        string moduleKey,
        string? subFeatureKey = null,
        CancellationToken ct = default)
    {
        var result = await IsEnabledAsync(tenantId, moduleKey, subFeatureKey, ct);
        if (!result.IsSuccess)
            return Result.Failure(result.Message ?? "Feature check failed.", result.StatusCode);

        if (result.Data)
            return Result.Success();

        var featureKey = TenantModuleFeatureKeys.Combine(moduleKey, subFeatureKey);
        return Result.Forbidden($"Feature disabled: '{featureKey}' is not enabled for this tenant.");
    }

    public void Invalidate(Guid tenantId, string moduleKey, string? subFeatureKey = null)
    {
        if (tenantId == Guid.Empty)
            return;

        var (normalizedModuleKey, normalizedSubFeatureKey) =
            TenantModuleFeatureKeys.Normalize(moduleKey, subFeatureKey);

        if (string.IsNullOrWhiteSpace(normalizedModuleKey))
            return;

        // Feature reads are intentionally uncached because authorization changes must
        // become visible across replicas immediately.
    }

}
