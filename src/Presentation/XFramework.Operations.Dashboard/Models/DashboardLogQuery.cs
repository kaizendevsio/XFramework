namespace XFramework.Operations.Dashboard.Models;

public sealed record DashboardLogQuery(
    string? Application,
    string? MachineName,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int Count);
