using System.Reflection;
using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.Configurations;

namespace XFramework.Integration.ServiceDiscovery;

public sealed class ConfigurationBoltServiceManifestProvider(
    IConfiguration configuration,
    IOptions<BoltConfiguration> boltConfiguration) : IBoltServiceManifestProvider
{
    public const string SectionName = "XFrameworkServiceManifest";

    public ValueTask<BoltServiceManifest?> GetManifestAsync(CancellationToken ct = default)
    {
        var manifest = configuration.GetSection(SectionName).Get<BoltServiceManifest>()
            ?? new BoltServiceManifest();

        var clientName = boltConfiguration.Value.ClientName ?? "unknown";
        if (string.IsNullOrWhiteSpace(manifest.ServiceName))
        {
            manifest.ServiceName = clientName;
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName))
        {
            manifest.DisplayName = manifest.ServiceName;
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            manifest.Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
        }

        manifest.Modules ??= [];
        manifest.Dependencies ??= [];
        manifest.Metadata ??= [];

        return ValueTask.FromResult<BoltServiceManifest?>(manifest);
    }
}
