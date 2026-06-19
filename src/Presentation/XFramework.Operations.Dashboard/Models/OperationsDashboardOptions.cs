namespace XFramework.Operations.Dashboard.Models;

public sealed class OperationsDashboardOptions
{
    public const string SectionName = "OperationsDashboard";

    public int RefreshSeconds { get; set; } = 10;
    public int DefaultLogCount { get; set; } = 50;
    public int DefaultTraceLimit { get; set; } = 20;
    public int DefaultLookbackMinutes { get; set; } = 30;

    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(Math.Max(5, RefreshSeconds));
    public TimeSpan DefaultLookback => TimeSpan.FromMinutes(Math.Max(5, DefaultLookbackMinutes));
}
