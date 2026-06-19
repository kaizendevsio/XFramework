namespace IdentityServer.Domain.Shared.Contracts;

public interface ITenantModuleFeatureCatalog
{
    IReadOnlyList<TenantModuleFeatureDefinition> All { get; }

    TenantModuleFeatureDefinition? Find(string moduleKey, string? subFeatureKey = null);
}
