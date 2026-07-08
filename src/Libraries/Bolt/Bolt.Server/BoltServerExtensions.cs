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

    /// <summary>Maximum complete Bolt frame size accepted by receive loops. Default: 100MB.</summary>
    public int MaxFrameBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>Bounded send queue capacity per connection. Default: 4096.</summary>
    public int SendQueueCapacity { get; set; } = 4096;

    /// <summary>Max time to wait for a send queue slot. 0 uses InvocationTimeoutMs.</summary>
    public int SendEnqueueTimeoutMs { get; set; } = 0;

    /// <summary>Controls whether authenticated service identities are bound to Bolt registration identities.</summary>
    public BoltRegistrationIdentityBindingMode RegistrationIdentityBindingMode { get; set; } =
        BoltRegistrationIdentityBindingMode.Audit;

    /// <summary>Scope required before an authenticated principal can register a reserved service identity.</summary>
    public string RequiredServiceScope { get; set; } = "bolt.service";

    /// <summary>Claims inspected, in order, to resolve the authenticated service name.</summary>
    public List<string> ServiceIdentityClaimTypes { get; } = ["client_id", "service", "azp", "sub"];

    /// <summary>Service names whose Bolt registrations are reserved for authenticated service principals.</summary>
    public List<string> ReservedServiceNames { get; } = [];

    /// <summary>Service name prefixes whose Bolt registrations are reserved for authenticated service principals.</summary>
    public List<string> ReservedServiceNamePrefixes { get; } = [];

    /// <summary>Client IDs whose Bolt registrations are reserved for authenticated service principals.</summary>
    public List<string> ReservedServiceClientIds { get; } = [];

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
            var durableStore = sp.GetService<Bolt.Server.Durable.IDurableQueueStore>();
            var durableOptions = sp.GetService<Microsoft.Extensions.Options.IOptions<Bolt.Server.Durable.DurableQueueOptions>>();
            var topicAuthorizers = sp.GetServices<IBoltTopicAuthorizer>();

            BoltServer server;
            if (durableStore is not null && durableOptions is not null)
                server = new BoltServer(logger, options, durableStore, durableOptions, topicAuthorizers);
            else
                server = new BoltServer(logger, options, topicAuthorizers);

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
    public static IEndpointConventionBuilder MapBolt(this IEndpointRouteBuilder endpoints, string path = "/bolt")
    {
        return endpoints.Map(path, async (HttpContext context) =>
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
            await server.HandleConnectionAsync(transport, context.User, context.RequestAborted);
        });
    }
}
