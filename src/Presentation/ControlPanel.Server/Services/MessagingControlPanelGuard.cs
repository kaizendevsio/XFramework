using IdentityServer.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;

namespace ControlPanel.Server.Services;

public sealed class MessagingControlPanelGuard(
    IDataContext dataContext,
    TenantFilterService tenantFilter)
{
    public async Task<MessagingControlPanelGuardResult> GetCurrentTenantStateAsync(
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return MessagingControlPanelGuardResult.NoTenantSelected();
        }

        var (moduleKey, subFeatureKey) = TenantModuleFeatureKeys.Normalize(
            TenantModuleFeatureKeys.Messaging,
            TenantModuleFeatureKeys.ChatSubFeature);

        var feature = await dataContext.Query<TenantModuleFeature>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId)
            .Where(x => x.ModuleKey == moduleKey)
            .Where(x => x.SubFeatureKey == subFeatureKey)
            .FirstOrDefaultAsync(ct);

        if (feature is null or { IsEnabled: false } or { IsDeleted: true })
        {
            return MessagingControlPanelGuardResult.ModuleDisabled(
                tenantId,
                tenantFilter.SelectedTenantName);
        }

        return MessagingControlPanelGuardResult.Enabled(
            tenantId,
            tenantFilter.SelectedTenantName);
    }
}

public sealed record MessagingControlPanelGuardResult(
    Guid? TenantId,
    string? TenantName,
    bool HasSelectedTenant,
    bool IsEnabled,
    string Title,
    string Description)
{
    public static MessagingControlPanelGuardResult Enabled(Guid tenantId, string? tenantName) =>
        new(
            tenantId,
            tenantName,
            HasSelectedTenant: true,
            IsEnabled: true,
            Title: "Messaging Enabled",
            Description: "Messaging diagnostics are available for the selected tenant.");

    public static MessagingControlPanelGuardResult NoTenantSelected() =>
        new(
            TenantId: null,
            TenantName: null,
            HasSelectedTenant: false,
            IsEnabled: false,
            Title: "Select a Tenant",
            Description: "Choose a tenant from the sidebar to view Messaging control panel pages.");

    public static MessagingControlPanelGuardResult ModuleDisabled(Guid tenantId, string? tenantName) =>
        new(
            tenantId,
            tenantName,
            HasSelectedTenant: true,
            IsEnabled: false,
            Title: "Messaging Disabled",
            Description: "Messaging Chat is disabled or not initialized for this tenant. Enable it from the tenant Modules tab before viewing diagnostics.");
}
