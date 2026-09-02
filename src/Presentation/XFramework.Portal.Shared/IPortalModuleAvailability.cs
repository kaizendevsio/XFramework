namespace XFramework.Portal.Shared;

public interface IPortalModuleAvailability
{
    Guid? ActiveTenantId { get; }
    bool HasConcreteTenant { get; }
    bool IsLoading { get; }

    event Action? OnChanged;

    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
    Task ReloadAsync(CancellationToken cancellationToken = default);
    bool IsFeatureEnabled(string moduleKey, string? subFeatureKey = null);
}
