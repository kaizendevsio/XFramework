using Bolt.Protocol.Transport;
using Bolt.Server.Media;
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

    /// <summary>
    /// Media processors that receive copies of media frames for server-side processing
    /// (recording, transcription, AI analysis, etc.).
    /// </summary>
    public List<IMediaProcessor> MediaProcessors { get; } = new();
}

public static class BoltServerExtensions
{
    /// <summary>
    /// Add Bolt server (hub) to the service collection.
    ///
    /// Usage:
    ///   builder.Services.AddBoltServer();
    ///   builder.Services.AddBoltServer(options => options.InvocationTimeoutMs = 60000);
    ///   builder.Services.AddBoltServer(options => options.MediaProcessors.Add(new MyRecorder()));
    /// </summary>
    public static IServiceCollection AddBoltServer(this IServiceCollection services, Action<BoltServerOptions>? configure = null)
    {
        var options = new BoltServerOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<BoltServer>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BoltServer>>();
            var server = new BoltServer(logger);
            foreach (var processor in options.MediaProcessors)
                server.RegisterMediaProcessor(processor);
            return server;
        });
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
            var transport = new WebSocketBoltConnection(webSocket);
            await server.HandleConnectionAsync(transport, context.RequestAborted);
        });

        return endpoints;
    }
}
