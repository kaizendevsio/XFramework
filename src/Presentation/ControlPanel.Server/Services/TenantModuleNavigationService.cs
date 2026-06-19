using IdentityServer.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;

namespace ControlPanel.Server.Services;

public sealed class TenantModuleNavigationService(
    TenantFilterService tenantFilter,
    IDataContext dataContext) : IDisposable
{
    private readonly Dictionary<string, bool> _features = new(StringComparer.OrdinalIgnoreCase);
    private Guid? _loadedTenantId;
    private bool _loading;

    public event Action? OnChanged;

    public Guid? ActiveTenantId => tenantFilter.SelectedTenantId;
    public bool HasConcreteTenant => tenantFilter.SelectedTenantId is Guid;
    public bool IsLoading => _loading;

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_loadedTenantId == tenantFilter.SelectedTenantId)
        {
            return;
        }

        await LoadAsync(ct);
    }

    public bool IsFeatureEnabled(string moduleKey, string? subFeatureKey = null)
    {
        var key = TenantModuleFeatureKeys.Combine(moduleKey, subFeatureKey);
        return tenantFilter.SelectedTenantId is Guid && _features.TryGetValue(key, out var enabled) && enabled;
    }

    public async Task ReloadAsync(CancellationToken ct = default) => await LoadAsync(ct);

    public void Initialize()
    {
        tenantFilter.OnChanged -= OnTenantFilterChanged;
        tenantFilter.OnChanged += OnTenantFilterChanged;
        _ = LoadAsync();
    }

    private void OnTenantFilterChanged()
    {
        _ = LoadAsync();
    }

    private async Task LoadAsync(CancellationToken ct = default)
    {
        if (_loading)
        {
            return;
        }

        _loading = true;

        try
        {
            _features.Clear();
            _loadedTenantId = tenantFilter.SelectedTenantId;

            if (tenantFilter.SelectedTenantId is not Guid tenantId)
            {
                return;
            }

            var rows = await dataContext.Query<TenantModuleFeature>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled)
                .OrderBy(x => x.ModuleKey)
                .ThenBy(x => x.SubFeatureKey)
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                _features[row.Key] = true;
            }
        }
        finally
        {
            _loading = false;
            OnChanged?.Invoke();
        }
    }

    public void Dispose()
    {
        tenantFilter.OnChanged -= OnTenantFilterChanged;
    }
}
