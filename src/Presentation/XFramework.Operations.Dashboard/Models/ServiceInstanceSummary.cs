namespace XFramework.Operations.Dashboard.Models;

public sealed record ServiceInstanceSummary(
    string ClientId,
    string ClientName,
    string ServiceName,
    string DisplayName,
    string? Version,
    string? Description,
    string Status,
    string StatusCssClass,
    int ConnectionCount,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastDisconnectedAt,
    string? MachineName,
    string TraceServiceName,
    IReadOnlyList<ServiceModuleSummary> Modules,
    IReadOnlyList<DependencySummary> Dependencies)
{
    public int MissingRequiredDependencies => Dependencies.Count(x => x is { Required: true, IsSatisfied: false });
    public bool HasRequiredDependencyFailures => MissingRequiredDependencies > 0;
    public string LogApplicationName => string.IsNullOrWhiteSpace(ClientName) ? ServiceName : ClientName;
}
