using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Storage.Api.Services.Providers;

namespace Storage.Api.Health;

public sealed class StorageProviderReadinessHealthCheck(
    IStorageProviderFactory providerFactory,
    IOptions<StorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.ReadinessTimeoutSeconds, 1, 30)));

        try
        {
            var provider = providerFactory.Resolve(options.Value.ResolveDefaultProviderKind());
            await provider.CheckReadinessAsync(timeout.Token);
            return HealthCheckResult.Healthy("Storage object provider is reachable.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Storage object provider readiness check timed out.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Storage object provider is unavailable.", ex);
        }
    }
}
