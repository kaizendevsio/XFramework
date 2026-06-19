namespace XFramework.Operations.Dashboard.Models;

public sealed record ServiceModuleSummary(
    string ModuleKey,
    string DisplayName,
    string Description,
    string? Version,
    string IconName,
    string Status,
    string StatusCssClass,
    int FeatureCount,
    IReadOnlyList<DependencySummary> Dependencies)
{
    public int MissingRequiredDependencies => Dependencies.Count(x => x is { Required: true, IsSatisfied: false });
}
