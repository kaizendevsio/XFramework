using Bolt.Domain.Shared.Contracts.ServiceDiscovery;

namespace XFramework.Operations.Dashboard.Services;

public static class OperationsRegistryMapper
{
    public static DashboardRegistrySnapshot CreateSnapshot(
        IEnumerable<BoltServiceRegistryItem> serviceItems,
        IEnumerable<BoltModuleRegistryItem> moduleItems,
        DateTimeOffset capturedAt)
    {
        var modulesByClientId = moduleItems
            .GroupBy(module => module.ClientId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var services = serviceItems
            .Select(service => MapService(service, modulesByClientId))
            .OrderBy(service => StatusSortValue(service.Status))
            .ThenBy(service => service.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var summary = new RegistryStatusSummary(
            services.Count,
            services.Count(x => string.Equals(x.Status, "Online", StringComparison.OrdinalIgnoreCase)),
            services.Count(x => string.Equals(x.Status, "Degraded", StringComparison.OrdinalIgnoreCase)),
            services.Count(x => string.Equals(x.Status, "Offline", StringComparison.OrdinalIgnoreCase)),
            services.Where(x => !string.Equals(x.Status, "Offline", StringComparison.OrdinalIgnoreCase))
                .Sum(x => Math.Max(0, x.ConnectionCount)));

        return new DashboardRegistrySnapshot(
            true,
            capturedAt,
            summary,
            services,
            []);
    }

    private static ServiceInstanceSummary MapService(
        BoltServiceRegistryItem service,
        IReadOnlyDictionary<string, List<BoltModuleRegistryItem>> modulesByClientId)
    {
        modulesByClientId.TryGetValue(service.ClientId, out var serviceModules);
        serviceModules ??= [];

        var dependencies = service.DependencyStatuses.Select(MapDependency).ToList();
        var status = ResolveStatus(service.Status, dependencies);

        service.Manifest.Metadata.TryGetValue("MachineName", out var machineName);

        return new ServiceInstanceSummary(
            service.ClientId,
            service.ClientName,
            service.ServiceName,
            FirstNonEmpty(service.DisplayName, service.ServiceName, service.ClientName, service.ClientId),
            service.Version,
            service.Manifest.Description,
            status,
            StatusCssClass(status),
            service.ConnectionCount,
            AsOffset(service.LastSeenAt),
            AsNullableOffset(service.LastConnectedAt),
            AsNullableOffset(service.LastDisconnectedAt),
            string.IsNullOrWhiteSpace(machineName) ? null : machineName,
            serviceModules.Select(MapModule).ToList(),
            dependencies);
    }

    private static ServiceModuleSummary MapModule(BoltModuleRegistryItem module)
    {
        var dependencies = module.DependencyStatuses.Select(MapDependency).ToList();
        var status = ResolveStatus(module.Status, dependencies);

        return new ServiceModuleSummary(
            module.ModuleKey,
            FirstNonEmpty(module.DisplayName, module.ModuleKey),
            module.Description,
            module.Version,
            string.IsNullOrWhiteSpace(module.IconName) ? "box" : module.IconName,
            status,
            StatusCssClass(status),
            module.Features.Count,
            dependencies);
    }

    private static DependencySummary MapDependency(BoltDependencyStatus dependency)
    {
        var requirement = dependency.Requirement;

        return new DependencySummary(
            requirement.Kind.ToString(),
            requirement.Key,
            FirstNonEmpty(requirement.DisplayName, requirement.Key),
            requirement.MinVersion,
            requirement.Required,
            dependency.IsSatisfied,
            dependency.MatchedKey,
            dependency.Message);
    }

    private static string ResolveStatus(BoltRegistryStatus status, IReadOnlyList<DependencySummary> dependencies)
    {
        if (status == BoltRegistryStatus.Offline)
        {
            return "Offline";
        }

        if (dependencies.Any(x => x is { Required: true, IsSatisfied: false }))
        {
            return "Degraded";
        }

        return status switch
        {
            BoltRegistryStatus.Online => "Online",
            BoltRegistryStatus.Degraded => "Degraded",
            _ => "Offline"
        };
    }

    public static string StatusCssClass(string status) =>
        status.ToLowerInvariant() switch
        {
            "online" => "status-online",
            "degraded" => "status-degraded",
            "offline" => "status-offline",
            _ => "status-unavailable"
        };

    private static int StatusSortValue(string status) =>
        status.ToLowerInvariant() switch
        {
            "degraded" => 0,
            "online" => 1,
            "offline" => 2,
            _ => 3
        };

    private static DateTimeOffset AsOffset(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return new DateTimeOffset(utc);
    }

    private static DateTimeOffset? AsNullableOffset(DateTime? value) =>
        value is null ? null : AsOffset(value.Value);

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
}
