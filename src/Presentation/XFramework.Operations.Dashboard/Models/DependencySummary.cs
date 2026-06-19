namespace XFramework.Operations.Dashboard.Models;

public sealed record DependencySummary(
    string Kind,
    string Key,
    string DisplayName,
    string? MinVersion,
    bool Required,
    bool IsSatisfied,
    string? MatchedKey,
    string Message);
