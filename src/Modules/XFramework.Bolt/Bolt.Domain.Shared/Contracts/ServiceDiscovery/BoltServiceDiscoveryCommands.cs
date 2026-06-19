namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

public static class BoltServiceDiscoveryCommands
{
    public const string AdvertiseServiceManifest = "__xframework.service_manifest.advertise";
    public const string GetServiceRegistry = "__xframework.service_registry.get";
    public const string GetModuleRegistry = "__xframework.module_registry.get";
}
