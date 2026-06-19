using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using XFramework.Core.Patterns;

namespace XFramework.Core.Services.FeatureGates;

public sealed class TenantModuleFeatureService(
    DbContext dbContext,
    IMemoryCache cache,
    ILogger<TenantModuleFeatureService> logger) : ITenantModuleFeatureService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

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

        var cacheKey = BuildCacheKey(tenantId, normalizedModuleKey, normalizedSubFeatureKey);
        if (cache.TryGetValue(cacheKey, out bool enabled))
        {
            logger.LogDebug(
                "Tenant module feature cache hit for feature {FeatureKey}",
                TenantModuleFeatureKeys.Combine(normalizedModuleKey, normalizedSubFeatureKey));

            return Result<bool>.Success(enabled);
        }

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

        enabled = dbEnabled ?? false;
        cache.Set(cacheKey, enabled, CacheDuration);

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

        cache.Remove(BuildCacheKey(tenantId, normalizedModuleKey, normalizedSubFeatureKey));
    }

    private static string BuildCacheKey(Guid tenantId, string moduleKey, string subFeatureKey) =>
        $"identity:tenant-module-feature:{tenantId:N}:{TenantModuleFeatureKeys.Combine(moduleKey, subFeatureKey)}";
}
