using IdentityServer.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;

namespace ControlPanel.Server.Services;

public sealed class CommunicationsControlPanelGuard(
    IDataContext dataContext,
    TenantFilterService tenantFilter)
{
    public async Task<CommunicationsControlPanelGuardResult> GetCurrentTenantStateAsync(
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return CommunicationsControlPanelGuardResult.NoTenantSelected();
        }

        var (moduleKey, subFeatureKey) = TenantModuleFeatureKeys.Normalize(TenantModuleFeatureKeys.Communications);

        var feature = await dataContext.Query<TenantModuleFeature>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.ModuleKey == moduleKey)
            .Where(x => x.SubFeatureKey == subFeatureKey)
            .FirstOrDefaultAsync(ct);

        if (feature is null or { IsEnabled: false } or { IsDeleted: true })
        {
            return CommunicationsControlPanelGuardResult.ModuleDisabled(
                tenantId,
                tenantFilter.SelectedTenantName);
        }

        return CommunicationsControlPanelGuardResult.Enabled(
            tenantId,
            tenantFilter.SelectedTenantName);
    }
}

public sealed record CommunicationsControlPanelGuardResult(
    Guid? TenantId,
    string? TenantName,
    bool HasSelectedTenant,
    bool IsEnabled,
    string Title,
    string Description)
{
    public static CommunicationsControlPanelGuardResult Enabled(Guid tenantId, string? tenantName) =>
        new(
            tenantId,
            tenantName,
            HasSelectedTenant: true,
            IsEnabled: true,
            Title: "Communications Enabled",
            Description: "Communications diagnostics are available for the selected tenant.");

    public static CommunicationsControlPanelGuardResult NoTenantSelected() =>
        new(
            TenantId: null,
            TenantName: null,
            HasSelectedTenant: false,
            IsEnabled: false,
            Title: "Select a Tenant",
            Description: "Choose a tenant from the sidebar to view Communications control panel pages.");

    public static CommunicationsControlPanelGuardResult ModuleDisabled(Guid tenantId, string? tenantName) =>
        new(
            tenantId,
            tenantName,
            HasSelectedTenant: true,
            IsEnabled: false,
            Title: "Communications Disabled",
            Description: "Communications is disabled or not initialized for this tenant. Enable it from the tenant Modules tab before viewing diagnostics.");
}
