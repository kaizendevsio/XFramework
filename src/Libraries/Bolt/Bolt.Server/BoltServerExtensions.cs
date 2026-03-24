using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bolt.Server;

public class BoltServerOptions
{
    /// <summary>Stale invocation cleanup interval in seconds. Default: 10.</summary>
    public int CleanupIntervalSeconds { get; set; } = 10;

    /// <summary>RPC invocation timeout in milliseconds. Default: 30000.</summary>
    public int InvocationTimeoutMs { get; set; } = 30_000;
}

public static class BoltServerExtensions
{
    /// <summary>
    /// Add Bolt server (hub) to the service collection.
    ///
    /// Usage:
    ///   builder.Services.AddBoltServer();
    ///   builder.Services.AddBoltServer(options => options.InvocationTimeoutMs = 60000);
    /// </summary>
    public static IServiceCollection AddBoltServer(this IServiceCollection services, Action<BoltServerOptions>? configure = null)
    {
        var options = new BoltServerOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<BoltServer>();
        return services;
    }

    /// <summary>
    /// Map the Bolt WebSocket endpoint.
    ///
    /// Usage:
    ///   app.UseWebSockets();
    ///   app.MapBolt();                    // defaults to "/bolt"
    ///   app.MapBolt("/custom/path");
    /// </summary>
    public static IEndpointRouteBuilder MapBolt(this IEndpointRouteBuilder endpoints, string path = "/bolt")
    {
        endpoints.Map(path, async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("WebSocket connections only");
                return;
            }

            var server = context.RequestServices.GetRequiredService<BoltServer>();
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await server.HandleConnectionAsync(webSocket, context.RequestAborted);
        });

        return endpoints;
    }
}
