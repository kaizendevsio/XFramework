using StreamFlow.Stream.Hubs;
using StreamFlow.Stream.ThinProtocol;

namespace StreamFlow.Stream.Extensions;

public static class ApplicationBuilderExtension
{
    public static IApplicationBuilder UseAppServices(this IApplicationBuilder appBuilder)
    {
        var app = appBuilder as WebApplication;

        // Thin binary WebSocket protocol — replaces SignalR
        app.UseWebSockets();
        app.MapBolt("/streamflow/ws");

        // Legacy SignalR hub — kept temporarily for existing tests/benchmarks during migration
        app.MapHub<MessageQueueHub>("/stream-flow/queue");

        return app as IApplicationBuilder;
    }
}