using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using XFramework.Core.Observability;

namespace XFramework.Core.Extensions;

/// <summary>
/// Extension methods for configuring OpenTelemetry distributed tracing and metrics
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Installs and configures OpenTelemetry with tracing and metrics for the application
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="serviceName">The service name for this module (e.g., "XFramework.Inventario.Api")</param>
    /// <param name="serviceVersion">The service version (default: "1.0.0")</param>
    public static IServiceCollection InstallOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        // Get configuration values
        var samplingProbability = configuration.GetValue<double>("OpenTelemetry:Sampling:Probability", 1.0);
        var consoleExporterEnabled = configuration.GetValue<bool>("OpenTelemetry:Exporters:Console:Enabled", false);
        var otlpExporterEnabled = configuration.GetValue<bool>("OpenTelemetry:Exporters:OTLP:Enabled", false);
        var otlpEndpoint = configuration.GetValue<string>("OpenTelemetry:Exporters:OTLP:Endpoint", "http://localhost:4317");

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    ["host.name"] = Environment.MachineName,
                    ["service.namespace"] = "XFramework"
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new TraceIdRatioBasedSampler(samplingProbability))
                    
                    // Automatic instrumentation
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.client_ip", request.HttpContext.Connection.RemoteIpAddress?.ToString());
                            activity.SetTag("http.user_agent", request.Headers.UserAgent.ToString());
                            activity.SetTag("http.request_id", request.HttpContext.TraceIdentifier);
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response_content_length", response.ContentLength);
                        };
                    })
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.request.method", request.Method.Method);
                            activity.SetTag("http.request.uri", request.RequestUri?.ToString());
                        };
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", (int)response.StatusCode);
                        };
                    });

                // Add Redis instrumentation if StackExchange.Redis is available
                try
                {
                    tracing.AddRedisInstrumentation();
                }
                catch
                {
                    // Redis instrumentation not available or not configured
                }

                // Add custom activity sources for business operations
                tracing.AddSource($"{ActivitySources.ServiceName}.*");

                // Configure exporters
                if (consoleExporterEnabled)
                {
                    tracing.AddConsoleExporter();
                }

                if (otlpExporterEnabled && !string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    // Automatic instrumentation
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    // Note: Runtime instrumentation requires OpenTelemetry.Instrumentation.Runtime package
                    // .AddRuntimeInstrumentation()
                    
                    // Custom metrics meter
                    .AddMeter("XFramework");

                // Configure exporters
                if (consoleExporterEnabled)
                {
                    metrics.AddConsoleExporter();
                }

                if (otlpExporterEnabled && !string.IsNullOrEmpty(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            });

        return services;
    }
}