using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

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
            AddIpPolicy(options, "auth", 10, TimeSpan.FromMinutes(1));

            // Strict policy for password reset: 3 requests per 15 minutes per IP
            AddIpPolicy(options, "password-reset", 3, TimeSpan.FromMinutes(15));

            AddIpPolicy(options, "verification", 5, TimeSpan.FromMinutes(15));

            // Standard API policy: 60 requests per minute per IP
            AddIpPolicy(options, "api", 60, TimeSpan.FromMinutes(1));

            options.RejectionStatusCode = 429;
        });

        return services;
    }

    /// <summary>
    /// Adds Redis-backed enforcement for security-sensitive IdentityServer HTTP routes.
    /// The existing ASP.NET Core policies remain active as defense in depth.
    /// </summary>
    public static IServiceCollection AddDistributedStrictSecurityRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(DistributedSecurityRateLimitOptions.SectionName);
        var configuredOptions = section.Get<DistributedSecurityRateLimitOptions>()
            ?? new DistributedSecurityRateLimitOptions();

        services.AddSingleton<IValidateOptions<DistributedSecurityRateLimitOptions>>(
            new DistributedSecurityRateLimitOptionsValidator(environment));
        services.AddOptions<DistributedSecurityRateLimitOptions>()
            .Bind(section)
            .ValidateOnStart();

        if (configuredOptions.Enabled)
        {
            services.TryAddSingleton<IConnectionMultiplexer>(serviceProvider =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<DistributedSecurityRateLimitOptions>>()
                    .Value;
                var redisConfiguration = ConfigurationOptions.Parse(options.RedisConnectionString!);
                redisConfiguration.AbortOnConnectFail = true;
                redisConfiguration.ConnectRetry = 1;
                redisConfiguration.ConnectTimeout = options.ConnectTimeoutMilliseconds;
                redisConfiguration.SyncTimeout = options.OperationTimeoutMilliseconds;
                redisConfiguration.AsyncTimeout = options.OperationTimeoutMilliseconds;
                try
                {
                    return ConnectionMultiplexer.Connect(redisConfiguration);
                }
                catch
                {
                    throw new InvalidOperationException(
                        "The distributed security rate-limit store is unavailable.");
                }
            });
            services.AddSingleton<IDistributedSecurityRateLimiter, RedisDistributedSecurityRateLimiter>();
            services.AddHostedService<DistributedSecurityRateLimitStartupService>();
        }
        else
        {
            services.AddSingleton<IDistributedSecurityRateLimiter, DisabledDistributedSecurityRateLimiter>();
        }

        return services;
    }

    private static void AddIpPolicy(
        RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window)
    {
        options.AddPolicy(policyName, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window
                }));
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

    /// <summary>
    /// Adds distributed strict security throttling before endpoint execution.
    /// </summary>
    public static IApplicationBuilder UseDistributedStrictSecurityRateLimiting(this IApplicationBuilder app)
    {
        app.UseMiddleware<DistributedSecurityRateLimitMiddleware>();
        return app;
    }
}
