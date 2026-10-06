using IdentityServer.Domain.Shared.Contracts;
using Bolt.Domain.Shared.Contracts.ServiceDiscovery;

namespace XFramework.Portal.Features.Identity.Services;

public sealed record ResolvedTenantModuleFeatureDefinition(
    TenantModuleFeatureDefinition Definition,
    IReadOnlyList<string> MissingRequiredDependencies,
    IReadOnlyList<string> MissingOptionalDependencies)
{
    public bool IsBlocked => MissingRequiredDependencies.Count > 0;
    public IReadOnlyList<BoltDependencyRequirement> Dependencies { get; init; } = [];
    public IReadOnlyList<string> UnavailableRequiredDependencies { get; init; } = [];
}
