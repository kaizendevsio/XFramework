namespace IdentityServer.Domain.Shared.Contracts;

public interface ITenantModuleFeatureDefinitionProvider
{
    IReadOnlyList<TenantModuleFeatureDefinition> Definitions { get; }
}
