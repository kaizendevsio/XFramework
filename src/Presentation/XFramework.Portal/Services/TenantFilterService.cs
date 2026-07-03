namespace XFramework.Portal.Services;

/// <summary>
/// Scoped service that holds the currently selected tenant filter from the sidebar.
/// Pages read this to filter queries; the layout writes it when the user changes the dropdown.
/// </summary>
public class TenantFilterService
{
    public Guid? SelectedTenantId { get; private set; }
    public string? SelectedTenantName { get; private set; }

    public event Action? OnChanged;
    public event Action? OnTenantsChanged;

    public void SetTenant(Guid? tenantId, string? tenantName)
    {
        SelectedTenantId = tenantId;
        SelectedTenantName = tenantName;
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        SelectedTenantId = null;
        SelectedTenantName = null;
        OnChanged?.Invoke();
    }

    public void NotifyTenantsChanged() => OnTenantsChanged?.Invoke();
}
