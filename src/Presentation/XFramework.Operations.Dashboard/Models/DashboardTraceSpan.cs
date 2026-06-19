namespace XFramework.Operations.Dashboard.Models;

public sealed record DashboardTraceSpan(
    string SpanId,
    string OperationName,
    string ServiceName,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    TimeSpan Offset,
    bool HasError);
