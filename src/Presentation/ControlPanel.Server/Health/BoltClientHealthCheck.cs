using Bolt.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ControlPanel.Server.Health;

public sealed class BoltClientHealthCheck(BoltClient client) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = client.IsConnected
            ? HealthCheckResult.Healthy("ControlPanel is connected to Bolt.")
            : HealthCheckResult.Unhealthy("ControlPanel is not connected to Bolt.");

        return Task.FromResult(result);
    }
}
