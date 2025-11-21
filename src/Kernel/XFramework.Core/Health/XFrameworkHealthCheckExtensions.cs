using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace XFramework.Core.Health;

/// <summary>
/// Extension methods for configuring XFramework health checks
/// </summary>
public static class XFrameworkHealthCheckExtensions
{
    /// <summary>
    /// Adds comprehensive health checks for XFramework applications
    /// </summary>
    public static IHealthChecksBuilder AddXFrameworkHealthChecks<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
        where TDbContext : DbContext
    {
        var healthChecksBuilder = services.AddHealthChecks();

        // Database health check
        healthChecksBuilder.AddDbContextCheck<TDbContext>(
            name: $"{serviceName}-database",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "database", "ready" });

        // Redis health check (if configured)
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            healthChecksBuilder.AddRedis(
                redisConnection,
                name: $"{serviceName}-redis",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "cache", "redis", "ready" });
        }

        // Memory health check
        healthChecksBuilder.AddCheck<MemoryHealthCheck>(
            "memory",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "memory", "live" });

        return healthChecksBuilder;
    }

    /// <summary>
    /// Configures health check endpoints with liveness and readiness probes
    /// </summary>
    public static IEndpointConventionBuilder MapXFrameworkHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string serviceName)
    {
        // Main health endpoint - all checks
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteResponse,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        });

        // Liveness probe - basic checks (is the app running?)
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = HealthCheckResponseWriter.WriteResponse,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        });

        // Readiness probe - all checks (is the app ready to serve traffic?)
        return endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = HealthCheckResponseWriter.WriteResponse,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        });
    }
}