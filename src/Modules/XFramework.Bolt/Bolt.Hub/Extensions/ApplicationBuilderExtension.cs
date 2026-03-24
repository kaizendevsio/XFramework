using Bolt.Server;
using Bolt.Hub.Hubs;

namespace Bolt.Hub.Extensions;

public static class ApplicationBuilderExtension
{
    public static IApplicationBuilder UseAppServices(this IApplicationBuilder appBuilder)
    {
        var app = appBuilder as WebApplication;

        // Bolt protocol endpoint
        app.UseWebSockets();
        app.MapBolt("/bolt/ws");

        // Legacy SignalR hub — kept temporarily for existing tests/benchmarks during migration
        app.MapHub<MessageQueueHub>("/stream-flow/queue");

        return app as IApplicationBuilder;
    }
}