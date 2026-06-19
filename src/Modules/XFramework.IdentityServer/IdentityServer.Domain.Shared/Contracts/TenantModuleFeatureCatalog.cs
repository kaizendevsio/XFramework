namespace IdentityServer.Domain.Shared.Contracts;

public sealed class TenantModuleFeatureCatalog : ITenantModuleFeatureCatalog
{
    private readonly IReadOnlyList<TenantModuleFeatureDefinition> _definitions;

    public TenantModuleFeatureCatalog(IEnumerable<ITenantModuleFeatureDefinitionProvider> providers)
    {
        _definitions = BuildDefinitions(providers);
    }

    public IReadOnlyList<TenantModuleFeatureDefinition> All => _definitions;

    public TenantModuleFeatureDefinition? Find(string moduleKey, string? subFeatureKey = null)
    {
        var key = TenantModuleFeatureKeys.Combine(moduleKey, subFeatureKey);
        return _definitions.FirstOrDefault(definition =>
            string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<TenantModuleFeatureDefinition> BuildDefinitions(
        IEnumerable<ITenantModuleFeatureDefinitionProvider> providers)
    {
        var definitions = new List<TenantModuleFeatureDefinition>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            foreach (var definition in provider.Definitions)
            {
                var normalizedDefinition = NormalizeDefinition(provider, definition);
                if (keys.Add(normalizedDefinition.Key))
                {
                    definitions.Add(normalizedDefinition);
                }
            }
        }

        return definitions;
    }

    private static TenantModuleFeatureDefinition NormalizeDefinition(
        ITenantModuleFeatureDefinitionProvider provider,
        TenantModuleFeatureDefinition definition)
    {
        var (moduleKey, subFeatureKey) =
            TenantModuleFeatureKeys.Normalize(definition.ModuleKey, definition.SubFeatureKey);

        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            throw new InvalidOperationException(
                $"{provider.GetType().FullName} returned a tenant module feature definition with an empty module key.");
        }

        return definition with
        {
            ModuleKey = moduleKey,
            SubFeatureKey = subFeatureKey
        };
    }
}
