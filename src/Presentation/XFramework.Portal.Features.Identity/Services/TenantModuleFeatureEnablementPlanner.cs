using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using IdentityServer.Domain.Shared.Contracts;

namespace XFramework.Portal.Features.Identity.Services;

public static class TenantModuleFeatureEnablementPlanner
{
    public static IReadOnlyList<TenantModuleFeatureDependencyIssue> FindIssues(
        IReadOnlyList<ResolvedTenantModuleFeatureDefinition> definitions, ISet<string> enabledKeys) =>
        definitions.Where(feature => enabledKeys.Contains(feature.Definition.Key))
            .Select(feature => new TenantModuleFeatureDependencyIssue(feature.Definition,
                Create(definitions, enabledKeys, [feature.Definition.Key], [])))
            .Where(issue => issue.Plan.EnableKeys.Count > 0 || issue.Plan.Errors.Count > 0)
            .ToList();

    public static IEnumerable<string> RequiredKeys(ResolvedTenantModuleFeatureDefinition feature)
    {
        if (!string.IsNullOrEmpty(feature.Definition.SubFeatureKey)) yield return feature.Definition.ModuleKey;
        foreach (var dependency in feature.Dependencies.Where(x => x.Required && x.Kind == BoltDependencyKind.TenantFeature))
            yield return TenantModuleFeatureKeys.Combine(dependency.Key);
    }

    public static TenantModuleFeatureEnablementPlan Create(
        IReadOnlyList<ResolvedTenantModuleFeatureDefinition> definitions,
        ISet<string> enabledKeys,
        IEnumerable<string> enableKeys,
        IEnumerable<string> disableKeys)
    {
        var byKey = definitions.ToDictionary(x => x.Definition.Key, StringComparer.OrdinalIgnoreCase);
        var disabled = disableKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enable = new List<string>();
        var errors = new List<string>();
        foreach (var key in enableKeys) Visit(key);

        var finalEnabled = enabledKeys.Except(disabled, StringComparer.OrdinalIgnoreCase)
            .Concat(enable).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var feature in definitions.Where(x => finalEnabled.Contains(x.Definition.Key)))
        foreach (var dependency in RequiredKeys(feature).Where(disabled.Contains))
            errors.Add($"{feature.Definition.DisplayName} requires {byKey.GetValueOrDefault(dependency)?.Definition.DisplayName ?? dependency}. Disable the dependent feature first.");

        return new(enable, disabled.ToList(), errors.Distinct().ToList());

        void Visit(string key)
        {
            key = TenantModuleFeatureKeys.Combine(key);
            if (!visited.Add(key)) return;
            if (!byKey.TryGetValue(key, out var feature))
            {
                errors.Add($"Required feature {key} is not available in the module catalog.");
                return;
            }
            if (disabled.Contains(key))
            {
                errors.Add($"{feature.Definition.DisplayName} cannot be enabled and disabled together.");
                return;
            }
            errors.AddRange(feature.UnavailableRequiredDependencies);
            foreach (var dependency in RequiredKeys(feature)) Visit(dependency);
            if (!enabledKeys.Contains(key)) enable.Add(key);
        }
    }
}
