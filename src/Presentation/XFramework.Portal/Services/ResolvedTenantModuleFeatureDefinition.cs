using IdentityServer.Domain.Shared.Contracts;

namespace XFramework.Portal.Services;

public sealed record ResolvedTenantModuleFeatureDefinition(
    TenantModuleFeatureDefinition Definition,
    IReadOnlyList<string> MissingRequiredDependencies,
    IReadOnlyList<string> MissingOptionalDependencies)
{
    public bool IsBlocked => MissingRequiredDependencies.Count > 0;
}
