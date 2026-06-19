namespace XFramework.Integration.ServiceDiscovery;

public sealed class BoltServiceDiscoveryOptions
{
    public const string SectionName = "BoltServiceDiscovery";

    public int ManifestRefreshSeconds { get; set; } = 30;
}
