using Bolt.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace XFramework.Portal.Health;

public sealed class BoltClientHealthCheck(BoltClient client) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = client.IsConnected
            ? HealthCheckResult.Healthy("Portal is connected to Bolt.")
            : HealthCheckResult.Unhealthy("Portal is not connected to Bolt.");

        return Task.FromResult(result);
    }
}
