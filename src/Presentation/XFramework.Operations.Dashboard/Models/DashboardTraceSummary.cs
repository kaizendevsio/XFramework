namespace XFramework.Operations.Dashboard.Models;

public sealed record DashboardTraceSummary(
    string TraceId,
    string RootOperation,
    string ServiceName,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    int SpanCount,
    bool HasErrors,
    IReadOnlyList<DashboardTraceSpan> Spans);
