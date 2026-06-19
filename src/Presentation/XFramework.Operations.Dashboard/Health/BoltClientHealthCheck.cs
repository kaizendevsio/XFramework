using Bolt.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace XFramework.Operations.Dashboard.Health;

public sealed class BoltClientHealthCheck(BoltClient client) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = client.IsConnected
            ? HealthCheckResult.Healthy("Operations Dashboard is connected to Bolt.")
            : HealthCheckResult.Unhealthy("Operations Dashboard is not connected to Bolt.");

        return Task.FromResult(result);
    }
}
