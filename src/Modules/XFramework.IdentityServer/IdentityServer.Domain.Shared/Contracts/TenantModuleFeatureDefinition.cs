namespace IdentityServer.Domain.Shared.Contracts;

public sealed record TenantModuleFeatureDefinition(
    string ModuleKey,
    string SubFeatureKey,
    string DisplayName,
    string Description,
    string IconName)
{
    public string Key => TenantModuleFeatureKeys.Combine(ModuleKey, SubFeatureKey);
}
