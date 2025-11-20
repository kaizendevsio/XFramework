using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace XFramework.Core.Middlewares;

/// <summary>
/// Middleware that generates and tracks correlation IDs for request tracing.
/// Adds correlation ID to response headers and Serilog log context.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private const string CorrelationIdItemKey = "CorrelationId";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // Try to get correlation ID from request header, otherwise generate new one
        var correlationId = GetOrCreateCorrelationId(context);

        // Store in HttpContext.Items for access by other middleware/services
        context.Items[CorrelationIdItemKey] = correlationId;

        // Add to response headers for client tracking
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);
            }
            return Task.CompletedTask;
        });

        // Push correlation ID to Serilog LogContext for structured logging
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        // Check if client provided a correlation ID
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationIdFromHeader)
            && !string.IsNullOrWhiteSpace(correlationIdFromHeader))
        {
            return correlationIdFromHeader.ToString();
        }

        // Generate new correlation ID
        return Guid.NewGuid().ToString("D");
    }
}

/// <summary>
/// Extension methods for registering CorrelationIdMiddleware.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Adds CorrelationIdMiddleware to the application pipeline.
    /// Should be registered early in the pipeline, before logging middleware.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Retrieves the correlation ID from HttpContext.Items.
    /// </summary>
    /// <param name="context">The HttpContext.</param>
    /// <returns>The correlation ID, or null if not found.</returns>
    public static string? GetCorrelationId(this HttpContext context)
    {
        if (context?.Items.TryGetValue("CorrelationId", out var correlationId) == true)
        {
            return correlationId?.ToString();
        }

        return null;
    }
}