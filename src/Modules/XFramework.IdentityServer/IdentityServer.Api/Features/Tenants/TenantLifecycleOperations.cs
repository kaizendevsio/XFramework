using XFramework.Core.Services;
using XFramework.Core.Services.FeatureGates;
using Microsoft.EntityFrameworkCore;
using FeatureGateService = XFramework.Core.Services.FeatureGates.ITenantModuleFeatureService;

namespace IdentityServer.Api.Features.Tenants;

internal static class TenantLifecycleOperations
{
    public static async Task RevokeActiveSessionsAsync(
        DbContext dbContext,
        Guid tenantId,
        CancellationToken ct)
    {
        var modifiedAt = DateTime.UtcNow;
        var concurrencyStamp = Guid.NewGuid();

        await dbContext.Set<Session>()
            .IgnoreQueryFilters()
            .Where(session => session.TenantId == tenantId)
            .Where(session => !session.IsDeleted && session.Status != CurrentSessionState.Inactive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.Status, CurrentSessionState.Inactive)
                .SetProperty(session => session.ModifiedAt, modifiedAt)
                .SetProperty(session => session.ConcurrencyStamp, concurrencyStamp), ct);
    }

    public static void Invalidate(
        Guid tenantId,
        ITenantResolver tenantResolver,
        FeatureGateService featureService)
    {
        tenantResolver.Invalidate(tenantId);
        foreach (var feature in TenantModuleFeatureKeys.All)
            featureService.Invalidate(tenantId, feature.ModuleKey, feature.SubFeatureKey);
    }
}
