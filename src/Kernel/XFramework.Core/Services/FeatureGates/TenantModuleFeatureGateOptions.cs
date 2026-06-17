using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;

namespace XFramework.Core.Services.FeatureGates;

public sealed class TenantModuleFeatureGateOptions
{
    public List<TenantModuleFeatureGateRule> Rules { get; } = [];

    public TenantModuleFeatureGateOptions RequireFeature(
        string moduleKey,
        string pathPrefix,
        string? subFeatureKey = null)
    {
        var (normalizedModuleKey, normalizedSubFeatureKey) =
            TenantModuleFeatureKeys.Normalize(moduleKey, subFeatureKey);

        Rules.Add(new TenantModuleFeatureGateRule(
            NormalizePathPrefix(pathPrefix),
            normalizedModuleKey,
            normalizedSubFeatureKey));

        return this;
    }

    private static string NormalizePathPrefix(string pathPrefix)
    {
        if (string.IsNullOrWhiteSpace(pathPrefix))
            throw new ArgumentException("Path prefix is required.", nameof(pathPrefix));

        var trimmed = pathPrefix.Trim();
        return trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
    }
}

public sealed record TenantModuleFeatureGateRule(
    string PathPrefix,
    string ModuleKey,
    string SubFeatureKey)
{
    public string FeatureKey => TenantModuleFeatureKeys.Combine(ModuleKey, SubFeatureKey);

    public bool Matches(PathString path) =>
        path.StartsWithSegments(new PathString(PathPrefix), StringComparison.OrdinalIgnoreCase);
}
