namespace IdentityServer.Domain.Shared.Contracts;

public sealed class BuiltInTenantModuleFeatureDefinitionProvider : ITenantModuleFeatureDefinitionProvider
{
    public IReadOnlyList<TenantModuleFeatureDefinition> Definitions => TenantModuleFeatureKeys.All;
}
