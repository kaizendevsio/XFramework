namespace XFramework.Operations.Dashboard.Models;

public sealed record DashboardLogEvent(
    DateTimeOffset Timestamp,
    string Level,
    string Message,
    string? SourceContext,
    string? Application,
    string? MachineName,
    IReadOnlyDictionary<string, string> Properties);
