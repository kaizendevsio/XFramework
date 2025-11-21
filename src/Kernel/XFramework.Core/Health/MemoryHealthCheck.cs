using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace XFramework.Core.Health;

/// <summary>
/// Health check that monitors application memory usage
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<MemoryCheckOptions> _options;

    public MemoryHealthCheck(IOptionsMonitor<MemoryCheckOptions> options)
    {
        _options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var allocated = GC.GetTotalMemory(forceFullCollection: false);
        var allocatedMb = allocated / 1024 / 1024;

        var data = new Dictionary<string, object>
        {
            { "AllocatedMB", allocatedMb },
            { "Gen0Collections", GC.CollectionCount(0) },
            { "Gen1Collections", GC.CollectionCount(1) },
            { "Gen2Collections", GC.CollectionCount(2) }
        };

        var status = allocatedMb < options.Threshold
            ? HealthStatus.Healthy
            : HealthStatus.Degraded;

        return Task.FromResult(new HealthCheckResult(
            status,
            description: $"Reports degraded status if allocated memory >= {options.Threshold} MB",
            data: data));
    }
}

/// <summary>
/// Configuration options for memory health check
/// </summary>
public class MemoryCheckOptions
{
    /// <summary>
    /// Memory threshold in MB. Default is 1024 MB (1 GB)
    /// </summary>
    public long Threshold { get; set; } = 1024;
}