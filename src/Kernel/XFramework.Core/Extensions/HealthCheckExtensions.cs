using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using XFramework.Core.HealthChecks;

namespace XFramework.Core.Extensions;

/// <summary>
/// Extension methods for configuring health checks.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds health check services including Redis health check.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddXFrameworkHealthChecks(this IServiceCollection services)
    {
        var healthChecksBuilder = services.AddHealthChecks();

        // Add Redis health check
        healthChecksBuilder.AddCheck<RedisHealthCheck>(
            name: "redis",
            failureStatus: HealthStatus.Degraded, // Degraded instead of Unhealthy for graceful degradation
            tags: new[] { "cache", "redis", "infrastructure" });

        // Can add more health checks here in the future:
        // - Database health check
        // - External API health check
        // - Disk space health check
        // - Memory health check

        return services;
    }

    /// <summary>
    /// Configures health check endpoints in the application pipeline.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseXFrameworkHealthChecks(this IApplicationBuilder app)
    {
        // Main health check endpoint - detailed response
        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteDetailedHealthCheckResponse,
            AllowCachingResponses = false
        });

        // Liveness probe - simple check that the app is running
        // Used by Kubernetes/orchestrators to determine if the pod is alive
        app.UseHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false, // Don't run any checks, just return 200 if app is running
            ResponseWriter = WriteLivenessResponse,
            AllowCachingResponses = false
        });

        // Readiness probe - checks if app is ready to receive traffic
        // Used by Kubernetes/orchestrators for load balancing decisions
        app.UseHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("infrastructure"),
            ResponseWriter = WriteReadinessResponse,
            AllowCachingResponses = false
        });

        return app;
    }

    /// <summary>
    /// Writes a detailed health check response with all check results.
    /// </summary>
    private static async Task WriteDetailedHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.TotalMilliseconds,
                exception = entry.Value.Exception?.Message,
                data = entry.Value.Data,
                tags = entry.Value.Tags
            }),
            timestamp = DateTime.UtcNow
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    /// <summary>
    /// Writes a simple liveness response.
    /// </summary>
    private static async Task WriteLivenessResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = "Alive",
            timestamp = DateTime.UtcNow
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    /// <summary>
    /// Writes a readiness response with infrastructure checks.
    /// </summary>
    private static async Task WriteReadinessResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            ready = report.Status == HealthStatus.Healthy,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                ready = entry.Value.Status == HealthStatus.Healthy
            }),
            timestamp = DateTime.UtcNow
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}