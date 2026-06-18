namespace IdentityServer.Domain.Shared.Contracts;

public sealed record TenantModuleFeatureDefinition(
    string ModuleKey,
    string SubFeatureKey,
    string DisplayName,
    string Description,
    string IconName,
    bool DefaultEnabled = true)
{
    public string Key => TenantModuleFeatureKeys.Combine(ModuleKey, SubFeatureKey);
}
