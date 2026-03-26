using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace XFramework.Core.RateLimiting;

/// <summary>
/// Extension methods for configuring rate limiting across XFramework services.
/// Uses ASP.NET Core's built-in rate limiting with fixed-window policies
/// partitioned by client IP address.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds XFramework rate limiting services with a global limiter and named policies.
    /// </summary>
    /// <remarks>
    /// Configures the following:
    /// <list type="bullet">
    ///   <item><description>Global limiter: 100 requests per minute per IP</description></item>
    ///   <item><description>"auth" policy: 10 requests per minute per IP (login, token refresh)</description></item>
    ///   <item><description>"password-reset" policy: 3 requests per 15 minutes per IP</description></item>
    ///   <item><description>"api" policy: 60 requests per minute per IP (general API endpoints)</description></item>
    /// </list>
    /// Register in Program.cs before building the app, then call <c>app.UseRateLimiter()</c>
    /// before <c>app.MapGeneratedEndpoints()</c>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddXFrameworkRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Global rate limit: 100 requests per minute per IP
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            // Strict policy for auth endpoints: 10 requests per minute per IP
            options.AddFixedWindowLimiter("auth", opt =>
            {
                opt.PermitLimit = 10;
                opt.Window = TimeSpan.FromMinutes(1);
            });

            // Strict policy for password reset: 3 requests per 15 minutes per IP
            options.AddFixedWindowLimiter("password-reset", opt =>
            {
                opt.PermitLimit = 3;
                opt.Window = TimeSpan.FromMinutes(15);
            });

            // Standard API policy: 60 requests per minute per IP
            options.AddFixedWindowLimiter("api", opt =>
            {
                opt.PermitLimit = 60;
                opt.Window = TimeSpan.FromMinutes(1);
            });

            options.RejectionStatusCode = 429;
        });

        return services;
    }

    /// <summary>
    /// Adds rate limiting middleware to the application pipeline.
    /// Must be called after <c>UseCorrelationId</c> and before <c>MapGeneratedEndpoints</c>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseXFrameworkRateLimiting(this IApplicationBuilder app)
    {
        app.UseRateLimiter();
        return app;
    }
}
