namespace XFramework.Operations.Dashboard.Models;

public sealed record DashboardRegistrySnapshot(
    bool IsConnected,
    DateTimeOffset CapturedAt,
    RegistryStatusSummary Summary,
    IReadOnlyList<ServiceInstanceSummary> Services,
    IReadOnlyList<string> Warnings)
{
    public static DashboardRegistrySnapshot EmptyDisconnected(string warning) =>
        new(
            false,
            DateTimeOffset.UtcNow,
            new RegistryStatusSummary(0, 0, 0, 0, 0),
            [],
            [warning]);
}
