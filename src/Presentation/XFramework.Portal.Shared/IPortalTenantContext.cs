namespace XFramework.Portal.Shared;

public interface IPortalTenantContext
{
    Guid? SelectedTenantId { get; }
    string? SelectedTenantName { get; }

    event Action? OnChanged;
    event Action? OnTenantsChanged;

    void NotifyTenantsChanged();
}
