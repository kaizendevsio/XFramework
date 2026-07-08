using Bolt.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace XFramework.Portal.Health;

public sealed class BoltClientHealthCheck(BoltClient client) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = client.GetHealthSnapshot();
        var data = new Dictionary<string, object>
        {
            ["isRegistered"] = snapshot.IsRegistered,
            ["connectionCount"] = snapshot.ConnectionCount,
            ["connectedTransports"] = snapshot.ConnectedTransports,
            ["pendingSends"] = snapshot.PendingSends,
            ["activeSends"] = snapshot.ActiveSends,
            ["maxActiveSendElapsedMs"] = snapshot.MaxActiveSendElapsedMs,
            ["runningSendLoops"] = snapshot.RunningSendLoops,
            ["runningReceiveLoops"] = snapshot.RunningReceiveLoops,
            ["faultedSendLoops"] = snapshot.FaultedSendLoops,
            ["faultedReceiveLoops"] = snapshot.FaultedReceiveLoops,
            ["pendingSendsUnhealthyThreshold"] = snapshot.PendingSendsUnhealthyThreshold,
            ["activeSendUnhealthyThresholdMs"] = snapshot.ActiveSendUnhealthyThresholdMs
        };

        var result = snapshot.IsHealthy
            ? HealthCheckResult.Healthy("Portal Bolt client transport and loops are healthy.", data)
            : HealthCheckResult.Unhealthy("Portal Bolt client transport or loops are unhealthy.", data: data);

        return Task.FromResult(result);
    }
}
