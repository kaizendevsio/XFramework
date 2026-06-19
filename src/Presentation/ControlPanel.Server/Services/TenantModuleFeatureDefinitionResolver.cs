using Bolt.Client;
using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using IdentityServer.Domain.Shared.Contracts;

namespace ControlPanel.Server.Services;

public sealed class TenantModuleFeatureDefinitionResolver(
    ITenantModuleFeatureCatalog localCatalog,
    BoltClient boltClient,
    ILogger<TenantModuleFeatureDefinitionResolver> logger)
{
    public async Task<IReadOnlyList<ResolvedTenantModuleFeatureDefinition>> ResolveAsync(
        IReadOnlyCollection<TenantModuleFeature> tenantFeatures,
        CancellationToken ct = default)
    {
        var definitions = new List<ResolvedTenantModuleFeatureDefinition>();
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in localCatalog.All)
        {
            AddDefinition(
                definitions,
                indexes,
                new ResolvedTenantModuleFeatureDefinition(definition, [], []));
        }

        var discoveredModules = await GetDiscoveredModulesAsync(ct);
        if (discoveredModules is null)
        {
            return definitions;
        }

        var enabledTenantFeatureKeys = tenantFeatures
            .Where(feature => feature.IsEnabled && !feature.IsDeleted)
            .Select(feature => feature.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var module in discoveredModules.Modules)
        {
            foreach (var feature in module.Features)
            {
                var resolved = CreateResolvedDefinition(module, feature, enabledTenantFeatureKeys);
                AddDefinition(definitions, indexes, resolved);
            }
        }

        return definitions;
    }

    private async Task<BoltModuleRegistryResponse?> GetDiscoveredModulesAsync(CancellationToken ct)
    {
        if (!boltClient.IsConnected)
        {
            return null;
        }

        try
        {
            return await boltClient.SendAsync<BoltModuleRegistryRequest, BoltModuleRegistryResponse>(
                string.Empty,
                BoltServiceDiscoveryCommands.GetModuleRegistry,
                new BoltModuleRegistryRequest { IncludeOffline = true },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to query Bolt module registry");
            return null;
        }
    }

    public static ResolvedTenantModuleFeatureDefinition CreateResolvedDefinition(
        BoltModuleRegistryItem module,
        BoltTenantModuleFeatureRegistryItem feature,
        ISet<string> enabledTenantFeatureKeys)
    {
        var (moduleKey, subFeatureKey) = NormalizeFeatureKey(feature.Key, feature.ModuleKey, feature.SubFeatureKey);
        var key = TenantModuleFeatureKeys.Combine(moduleKey, subFeatureKey);
        var missingRequired = new List<string>();
        var missingOptional = new List<string>();

        if (feature.Status == BoltRegistryStatus.Offline)
        {
            missingRequired.Add($"Service {module.ServiceName} is offline.");
        }

        foreach (var status in feature.DependencyStatuses)
        {
            if (status.Requirement.Kind == BoltDependencyKind.TenantFeature)
            {
                continue;
            }

            if (!status.IsSatisfied)
            {
                AddDependencyMessage(status, missingRequired, missingOptional);
            }
        }

        foreach (var dependency in feature.Dependencies.Where(x => x.Kind == BoltDependencyKind.TenantFeature))
        {
            var dependencyKey = TenantModuleFeatureKeys.Combine(dependency.Key);
            var isSatisfied = enabledTenantFeatureKeys.Contains(dependencyKey);
            if (isSatisfied)
            {
                continue;
            }

            var message = $"{GetDependencyDisplayName(dependency)} is not enabled for this tenant.";
            if (dependency.Required)
            {
                missingRequired.Add(message);
            }
            else
            {
                missingOptional.Add(message);
            }
        }

        var isBlocked = missingRequired.Count > 0;
        var definition = new TenantModuleFeatureDefinition(
            moduleKey,
            subFeatureKey,
            string.IsNullOrWhiteSpace(feature.DisplayName) ? key : feature.DisplayName,
            feature.Description,
            string.IsNullOrWhiteSpace(feature.IconName) ? module.IconName : feature.IconName,
            feature.DefaultEnabled && !isBlocked);

        return new ResolvedTenantModuleFeatureDefinition(definition, missingRequired, missingOptional);
    }

    private static void AddDependencyMessage(
        BoltDependencyStatus status,
        ICollection<string> missingRequired,
        ICollection<string> missingOptional)
    {
        var message = string.IsNullOrWhiteSpace(status.Message)
            ? $"{GetDependencyDisplayName(status.Requirement)} is not available."
            : status.Message;

        if (status.Requirement.Required)
        {
            missingRequired.Add(message);
        }
        else
        {
            missingOptional.Add(message);
        }
    }

    private static string GetDependencyDisplayName(BoltDependencyRequirement dependency) =>
        string.IsNullOrWhiteSpace(dependency.DisplayName)
            ? dependency.Key
            : dependency.DisplayName;

    private static void AddDefinition(
        IList<ResolvedTenantModuleFeatureDefinition> definitions,
        IDictionary<string, int> indexes,
        ResolvedTenantModuleFeatureDefinition resolved)
    {
        if (!indexes.TryGetValue(resolved.Definition.Key, out var index))
        {
            indexes[resolved.Definition.Key] = definitions.Count;
            definitions.Add(resolved);
            return;
        }

        definitions[index] = MergeResolvedDefinition(definitions[index], resolved);
    }

    public static ResolvedTenantModuleFeatureDefinition MergeResolvedDefinition(
        ResolvedTenantModuleFeatureDefinition existing,
        ResolvedTenantModuleFeatureDefinition discovered)
    {
        var missingRequired = existing.MissingRequiredDependencies
            .Concat(discovered.MissingRequiredDependencies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var missingOptional = existing.MissingOptionalDependencies
            .Concat(discovered.MissingOptionalDependencies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return existing with
        {
            MissingRequiredDependencies = missingRequired,
            MissingOptionalDependencies = missingOptional
        };
    }

    private static (string ModuleKey, string SubFeatureKey) NormalizeFeatureKey(
        string? key,
        string moduleKey,
        string subFeatureKey)
    {
        if (!string.IsNullOrWhiteSpace(moduleKey))
        {
            return TenantModuleFeatureKeys.Normalize(moduleKey, subFeatureKey);
        }

        return TenantModuleFeatureKeys.Normalize(key ?? string.Empty);
    }
}
