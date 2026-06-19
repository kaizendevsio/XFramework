namespace XFramework.Operations.Dashboard.Models;

public sealed record RegistryStatusSummary(
    int TotalServices,
    int OnlineServices,
    int DegradedServices,
    int OfflineServices,
    int ActiveInstances);
