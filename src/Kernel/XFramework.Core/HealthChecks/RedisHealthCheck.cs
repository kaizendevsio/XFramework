using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace XFramework.Core.HealthChecks;

/// <summary>
/// Health check for Redis connection.
/// Verifies that Redis is reachable and responding to commands.
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisHealthCheck> _logger;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(2);

    public RedisHealthCheck(
        IConnectionMultiplexer? redis,
        ILogger<RedisHealthCheck> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If Redis is not configured, report as degraded (not unhealthy)
            if (_redis == null)
            {
                return HealthCheckResult.Degraded(
                    "Redis is not configured. Application running in memory-only cache mode.",
                    data: new Dictionary<string, object>
                    {
                        ["configured"] = false,
                        ["mode"] = "memory-only"
                    });
            }

            // Check if connected
            if (!_redis.IsConnected)
            {
                return HealthCheckResult.Unhealthy(
                    "Redis connection is not established.",
                    data: new Dictionary<string, object>
                    {
                        ["connected"] = false,
                        ["endpoints"] = string.Join(", ", _redis.GetEndPoints().Select(ep => ep.ToString()))
                    });
            }

            // Perform ping test with timeout
            var db = _redis.GetDatabase();
            var startTime = DateTime.UtcNow;
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            try
            {
                var pingTime = await db.PingAsync();
                var totalLatency = DateTime.UtcNow - startTime;

                // Check if latency is acceptable
                if (pingTime.TotalMilliseconds > 1000)
                {
                    return HealthCheckResult.Degraded(
                        $"Redis is responding but slowly (ping: {pingTime.TotalMilliseconds:F2}ms)",
                        data: new Dictionary<string, object>
                        {
                            ["connected"] = true,
                            ["ping_ms"] = pingTime.TotalMilliseconds,
                            ["latency_ms"] = totalLatency.TotalMilliseconds,
                            ["endpoints"] = string.Join(", ", _redis.GetEndPoints().Select(ep => ep.ToString()))
                        });
                }

                return HealthCheckResult.Healthy(
                    $"Redis is healthy (ping: {pingTime.TotalMilliseconds:F2}ms)",
                    data: new Dictionary<string, object>
                    {
                        ["connected"] = true,
                        ["ping_ms"] = pingTime.TotalMilliseconds,
                        ["latency_ms"] = totalLatency.TotalMilliseconds,
                        ["endpoints"] = string.Join(", ", _redis.GetEndPoints().Select(ep => ep.ToString())),
                        ["server_version"] = GetServerVersion(_redis)
                    });
            }
            catch (OperationCanceledException)
            {
                return HealthCheckResult.Unhealthy(
                    $"Redis ping timed out after {_timeout.TotalSeconds} seconds",
                    data: new Dictionary<string, object>
                    {
                        ["connected"] = _redis.IsConnected,
                        ["timeout_seconds"] = _timeout.TotalSeconds
                    });
            }
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis connection health check failed");
            return HealthCheckResult.Unhealthy(
                "Redis connection failed",
                ex,
                data: new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["error_type"] = "RedisConnectionException"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check encountered an unexpected error");
            return HealthCheckResult.Unhealthy(
                "Redis health check failed with unexpected error",
                ex,
                data: new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["error_type"] = ex.GetType().Name
                });
        }
    }

    private static string GetServerVersion(IConnectionMultiplexer redis)
    {
        try
        {
            var server = redis.GetServer(redis.GetEndPoints().First());
            return server.Version.ToString();
        }
        catch
        {
            return "unknown";
        }
    }
}