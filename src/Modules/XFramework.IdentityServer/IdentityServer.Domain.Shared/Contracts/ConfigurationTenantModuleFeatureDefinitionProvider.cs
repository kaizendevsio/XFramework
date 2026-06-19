using Microsoft.Extensions.Configuration;

namespace IdentityServer.Domain.Shared.Contracts;

public sealed class ConfigurationTenantModuleFeatureDefinitionProvider : ITenantModuleFeatureDefinitionProvider
{
    public const string SectionName = "TenantModuleFeatures:Definitions";

    public ConfigurationTenantModuleFeatureDefinitionProvider(IConfiguration configuration)
    {
        Definitions = BuildDefinitions(configuration.GetSection(SectionName));
    }

    public IReadOnlyList<TenantModuleFeatureDefinition> Definitions { get; }

    private static IReadOnlyList<TenantModuleFeatureDefinition> BuildDefinitions(IConfigurationSection section)
    {
        var definitions = new List<TenantModuleFeatureDefinition>();

        foreach (var child in section.GetChildren())
        {
            var moduleKey = child["ModuleKey"] ?? string.Empty;
            var subFeatureKey = child["SubFeatureKey"] ?? string.Empty;
            var combinedKey = child["Key"];

            if (string.IsNullOrWhiteSpace(moduleKey) && !string.IsNullOrWhiteSpace(combinedKey))
            {
                (moduleKey, subFeatureKey) = TenantModuleFeatureKeys.Normalize(combinedKey);
            }

            var key = TenantModuleFeatureKeys.Combine(moduleKey, subFeatureKey);
            definitions.Add(new TenantModuleFeatureDefinition(
                moduleKey,
                subFeatureKey,
                string.IsNullOrWhiteSpace(child["DisplayName"]) ? key : child["DisplayName"]!,
                child["Description"] ?? string.Empty,
                string.IsNullOrWhiteSpace(child["IconName"]) ? "box" : child["IconName"]!,
                !bool.TryParse(child["DefaultEnabled"], out var defaultEnabled) || defaultEnabled));
        }

        return definitions;
    }
}
