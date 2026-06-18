using IdentityServer.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;

namespace ControlPanel.Server.Services;

public sealed class CommunityControlPanelAccessService(
    IDataContext dataContext,
    TenantFilterService tenantFilter)
{
    public async Task<CommunityControlPanelAccessState> GetStateAsync(
        bool includeNotifications = false,
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return new CommunityControlPanelAccessState(null, null, false, null);
        }

        var communityEnabled = await IsFeatureEnabledAsync(
            tenantId,
            TenantModuleFeatureKeys.Community,
            ct: ct);

        bool? notificationsEnabled = null;
        if (includeNotifications)
        {
            notificationsEnabled = await IsFeatureEnabledAsync(
                tenantId,
                TenantModuleFeatureKeys.Notifications,
                ct: ct);
        }

        return new CommunityControlPanelAccessState(
            tenantId,
            tenantFilter.SelectedTenantName,
            communityEnabled,
            notificationsEnabled);
    }

    private async Task<bool> IsFeatureEnabledAsync(
        Guid tenantId,
        string moduleKey,
        string? subFeatureKey = null,
        CancellationToken ct = default)
    {
        var (normalizedModuleKey, normalizedSubFeatureKey) =
            TenantModuleFeatureKeys.Normalize(moduleKey, subFeatureKey);

        var feature = await dataContext.Query<TenantModuleFeature>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x =>
                x.TenantId == tenantId &&
                x.ModuleKey == normalizedModuleKey &&
                x.SubFeatureKey == normalizedSubFeatureKey &&
                x.IsDeleted == false)
            .FirstOrDefaultAsync(ct);

        return feature?.IsEnabled == true;
    }
}

public sealed record CommunityControlPanelAccessState(
    Guid? TenantId,
    string? TenantName,
    bool CommunityEnabled,
    bool? NotificationsEnabled)
{
    public bool HasTenant => TenantId is not null;
    public bool CanUseCommunity => HasTenant && CommunityEnabled;
    public bool CanUseNotifications => CanUseCommunity && NotificationsEnabled == true;
}
