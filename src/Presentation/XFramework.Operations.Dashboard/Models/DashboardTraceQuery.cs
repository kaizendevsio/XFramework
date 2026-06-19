namespace XFramework.Operations.Dashboard.Models;

public sealed record DashboardTraceQuery(
    string ServiceName,
    TimeSpan Lookback,
    int Limit);
