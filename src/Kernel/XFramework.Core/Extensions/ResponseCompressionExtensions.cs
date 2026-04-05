using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;

namespace XFramework.Core.Extensions;

/// <summary>
/// Extension methods for configuring response compression.
/// Compresses HTTP responses to reduce bandwidth and improve performance.
/// </summary>
/// <remarks>
/// Response compression reduces the size of HTTP responses by compressing them before sending.
/// Modern browsers support Brotli (better compression) and Gzip (universal compatibility).
/// 
/// Important: Compression middleware must be added BEFORE output caching middleware
/// in the pipeline so that cached responses are already compressed.
/// </remarks>
public static class ResponseCompressionExtensions
{
    /// <summary>
    /// Adds response compression services with Brotli and Gzip providers.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// // In Program.cs
    /// services.AddConfiguredResponseCompression();
    /// 
    /// // Then in middleware pipeline (before UseOutputCache):
    /// app.UseResponseCompression();
    /// </code>
    /// </example>
    public static IServiceCollection AddConfiguredResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            // Enable compression for HTTPS requests (disabled by default for security)
            // Safe for APIs that don't include sensitive data in response bodies
            options.EnableForHttps = true;

            // Add compression providers in order of preference
            // Brotli first (better compression), Gzip as fallback
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();

            // Configure MIME types to compress
            // Default types + additional API-relevant types
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                // JSON APIs
                "application/json",
                "application/json; charset=utf-8",
                
                // XML APIs
                "application/xml",
                "text/xml",
                
                // Web content
                "text/html",
                "text/css",
                "text/plain",
                "text/javascript",
                "application/javascript",
                
                // Fonts
                "font/woff",
                "font/woff2",
                "application/font-woff",
                "application/font-woff2",
                
                // SVG
                "image/svg+xml",
                
                // Other
                "application/x-font-ttf",
                "application/x-font-opentype",
                "application/vnd.ms-fontobject"
            });
        });

        // Configure Brotli compression level
        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            // Optimal provides good balance between compression ratio and speed
            // Alternatives: Fastest (less compression, faster), SmallestSize (best compression, slower)
            options.Level = CompressionLevel.Optimal;
        });

        // Configure Gzip compression level
        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            // Optimal for general use
            options.Level = CompressionLevel.Optimal;
        });

        return services;
    }

    /// <summary>
    /// Adds response compression middleware to the pipeline.
    /// Must be called AFTER exception handling and HTTPS redirection,
    /// but BEFORE output caching.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseConfiguredResponseCompression(this IApplicationBuilder app)
    {
        app.UseResponseCompression();
        return app;
    }
}